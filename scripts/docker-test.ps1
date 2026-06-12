<#
.SYNOPSIS
    Build and smoke-test Articulate Docker images for any Umbraco target version.

.DESCRIPTION
    Two-phase test per target:

    Phase 1 - BackofficeDevelopment
        Starts Umbraco + Caddy. Waits for /umbraco (unattended install + package migrations).
        Runs smoke.mjs publish to publish the Articulate content tree via the Management API,
        then smoke.mjs confirm to verify all children are published and / returns 200.

    Phase 2 - Production
        Recreates the app container in Production mode, keeping the SQLite DB volume from
        Phase 1 so previously-published content is already in place.
        Runs smoke.mjs smoke to confirm / returns 200 without dev automation,
        then smoke.mjs theme to verify the Management API theme round-trip.

    Requires node on PATH (or NODE_BIN env var) for smoke.mjs.
    Use -SkipSmoke to fall back to the basic HTTP probe only.

    Set ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET to override the dev automation secret
    (default: articulate-dev-local-secret, matches the docker-compose.yml default).

.EXAMPLE
    $env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET = 'articulate-dev-local-secret'
    .\scripts\docker-test.ps1 -Target umbraco17
    .\scripts\docker-test.ps1 -Target umbraco18
    .\scripts\docker-test.ps1 -Target all -Keep    # leaves containers running for manual browsing

.NOTES
    Target configuration is defined in $TargetConfigs below.

    Umbraco 16 testing uses the local test site (Articulate.Tests.Website with net9.0).
    Docker tests cover Umbraco 17 (legacy lane) and 18 (umbraco18 lane) only.

    Ports:
      umbraco17 -> https://localhost:17017/
      umbraco18 -> https://localhost:18018/
#>
Param(
	[ValidateSet('umbraco17','umbraco18','all')]
	[string]$Target = 'all',
	[switch]$Keep,
	[switch]$SkipSmoke
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Configuration = if ($env:BUILD_CONFIGURATION) { $env:BUILD_CONFIGURATION } else { 'Release' }
$PackDir = Join-Path $RepoRoot "build\$Configuration"

# ── Target configuration matrix ──────────────────────────────────────────────
# Docker tests cover Umbraco 17 and 18 only (ArticulateDockerSite targets net10.0).
# Umbraco 16 testing uses the local test site (Articulate.Tests.Website with net9.0).
$TargetConfigs = @{
	umbraco17 = @{
		PackageLane     = 'legacy'
		TFM             = 'net10.0'
		UmbracoVersion  = '[17.2.2,18.0.0)'
		SdkImage        = 'mcr.microsoft.com/dotnet/sdk:10.0'
		AspnetImage     = 'mcr.microsoft.com/dotnet/aspnet:10.0'
		ImageTag        = 'articulate-local:umbraco17'
		ContainerPort   = 17017
	}
	umbraco18 = @{
		PackageLane     = 'umbraco18'
		TFM             = 'net10.0'
		UmbracoVersion  = '18.0.0-rc*'
		SdkImage        = 'mcr.microsoft.com/dotnet/sdk:10.0'
		AspnetImage     = 'mcr.microsoft.com/dotnet/aspnet:10.0'
		ImageTag        = 'articulate-local:umbraco18'
		ContainerPort   = 18018
	}
}

# ── Helpers ───────────────────────────────────────────────────────────────────

function Ensure-Packages {
	param([string]$PackRoot, [string]$Lane)
	$laneDir = Join-Path $PackRoot $Lane
	$hasArticulatePackage = Test-Path (Join-Path $laneDir 'Articulate.*.nupkg')
	$hasSampleThemePackage = Test-Path (Join-Path $laneDir 'Articulate.Theme.Sample.*.nupkg')
	if ($hasArticulatePackage -and $hasSampleThemePackage) {
		return
	}
	Write-Host "Required Docker packages missing under $laneDir. Building lane '$Lane'..."
	$psBuild = Join-Path $RepoRoot 'build\build.ps1'
	if (Test-Path $psBuild) {
		$previousLane = $env:ARTICULATE_PACKAGE_LANE
		$previousPackSampleTheme = $env:PACK_SAMPLE_THEME
		$env:ARTICULATE_PACKAGE_LANE = $Lane
		$env:PACK_SAMPLE_THEME = 'true'
		try {
			& pwsh -NoProfile -ExecutionPolicy Bypass -File $psBuild
			if ($LASTEXITCODE -ne 0) { throw "Package build failed with exit code $LASTEXITCODE" }
		}
		finally {
			$env:ARTICULATE_PACKAGE_LANE = $previousLane
			$env:PACK_SAMPLE_THEME = $previousPackSampleTheme
		}
	}
	else {
		& bash -lc "(cd '$RepoRoot' && ARTICULATE_PACKAGE_LANE=$Lane PACK_SAMPLE_THEME=true ./build/build.sh)"
		if ($LASTEXITCODE -ne 0) { throw "Package build failed with exit code $LASTEXITCODE" }
	}
}

function Build-And-Test {
	param(
		[string]$TargetName,
		[hashtable]$Config
	)

	$lane = $Config.PackageLane
	$tfm = if ($env:TARGET_FRAMEWORK) { $env:TARGET_FRAMEWORK } else { $Config.TFM }
	$umbVer = if ($env:UMBRACO_CMS_VERSION) { $env:UMBRACO_CMS_VERSION } else { $Config.UmbracoVersion }
	$sdkImage = if ($env:DOTNET_SDK_IMAGE) { $env:DOTNET_SDK_IMAGE } else { $Config.SdkImage }
	$aspnetImage = if ($env:DOTNET_ASPNET_IMAGE) { $env:DOTNET_ASPNET_IMAGE } else { $Config.AspnetImage }
	$imgTag = $Config.ImageTag
	$port = $Config.ContainerPort
	$pkgSrc = Join-Path $PackDir $lane
	$dockerPkgSrc = "build/$Configuration/$lane"

	if (-not (Test-Path $pkgSrc)) {
		Write-Error "Package source '$pkgSrc' not found; ensure build produced packages for lane=$lane"
		return 1
	}

	Write-Host ""
	Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
	Write-Host "  Target: $TargetName  |  Lane: $lane  |  TFM: $tfm" -ForegroundColor Cyan
	Write-Host "  Umbraco: $umbVer" -ForegroundColor Cyan
	Write-Host "  Image: $imgTag" -ForegroundColor Cyan
	Write-Host "  HTTPS smoke test: https://localhost:$port/" -ForegroundColor Cyan
	Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
	Write-Host ""

	# ── Build Docker image ────────────────────────────────────────────────────
	Write-Host "Building Docker image $imgTag ..."
	$buildArgs = @(
		'--build-arg', "DOTNET_SDK_IMAGE=$sdkImage",
		'--build-arg', "DOTNET_ASPNET_IMAGE=$aspnetImage",
		'--build-arg', "TARGET_FRAMEWORK=$tfm",
		'--build-arg', "UMBRACO_CMS_VERSION=$umbVer",
		'--build-arg', "PACKAGE_SOURCE=$dockerPkgSrc",
		'--build-arg', "PACKAGE_LANE=$lane"
	)
	& docker build --no-cache --pull @buildArgs -t $imgTag $RepoRoot
	if ($LASTEXITCODE -ne 0) {
		Write-Host "Docker build failed for $imgTag" -ForegroundColor Red
		return $LASTEXITCODE
	}

	# ── Run Umbraco + Caddy for HTTPS smoke test ─────────────────────────────
	$containerName = "art-$TargetName"
	$caddyName = "art-caddy-$TargetName"
	$networkName = "art-net-$TargetName"

	# Create isolated network so Caddy can resolve the app container
	docker network create $networkName 2>$null | Out-Null

	# Umbraco app container (HTTP internally, Production mode with UseHttps)
	$appEnvVars = @(
		'-e', 'ASPNETCORE_ENVIRONMENT=Container',
		'-e', 'ASPNETCORE_URLS=http://+:8080',
		'-e', 'ConnectionStrings__umbracoDbDSN=Data Source=/app/umbraco/Data/Umbraco.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True',
		'-e', 'ConnectionStrings__umbracoDbDSN_ProviderName=Microsoft.Data.Sqlite',
		# Production HTTPS requirements (Caddy terminates TLS)
		'-e', 'Umbraco__CMS__Global__UseHttps=true',
		'-e', "Umbraco__CMS__WebRouting__UmbracoApplicationUrl=https://localhost:$port/",
		'-e', "Umbraco__CMS__Security__BackOfficeHost=https://localhost:$port",
		# Unattended install
		'-e', 'Umbraco__CMS__Unattended__InstallUnattended=true',
		'-e', 'Umbraco__CMS__Unattended__UpgradeUnattended=true',
		'-e', 'Umbraco__CMS__Unattended__PackageMigrationsUnattended=true',
		'-e', "Umbraco__CMS__Unattended__UnattendedUserName=$(if ($env:UMBRACO_USER_NAME) { $env:UMBRACO_USER_NAME } else { 'Smoke Test' })",
		'-e', "Umbraco__CMS__Unattended__UnattendedUserEmail=$(if ($env:UMBRACO_USER_EMAIL) { $env:UMBRACO_USER_EMAIL } else { "smoke@$TargetName.localhost" })",
		'-e', "Umbraco__CMS__Unattended__UnattendedUserPassword=$(if ($env:UMBRACO_USER_PASSWORD) { $env:UMBRACO_USER_PASSWORD } else { '@rticulate!' })",
		'-e', 'Umbraco__CMS__ModelsBuilder__ModelsMode=Nothing'
	)

	Write-Host "Starting Umbraco container $containerName ..."
	& docker run -d --rm --name $containerName --network $networkName @appEnvVars $imgTag | Out-Null

	# Caddy sidecar for TLS termination
	$caddyfile = Join-Path $RepoRoot 'build/docker-site/Caddyfile'
	# Caddyfile listens on :18443 inside the container; we map host port to it
	Write-Host "Starting Caddy sidecar $caddyName on https://localhost:$port ..."
	& docker run -d --rm --name $caddyName --network $networkName `
		-p "${port}:18443" `
		-v "${caddyfile}:/etc/caddy/Caddyfile:ro" `
		-e "ARTICULATE_HOST=$containerName" `
		-e "HTTPS_HOST=localhost:$port" `
		caddy:2-alpine `
		caddy run --config /etc/caddy/Caddyfile --adapter caddyfile 2>&1 | Out-Null

	try {
		# Phase 1: BackofficeDevelopment - install + publish + confirm
		Write-Host ""
		Write-Host "Phase 1: BackofficeDevelopment (install, publish, confirm)" -ForegroundColor Cyan
		Write-Host "────────────────────────────────────────────────────────────" -ForegroundColor Cyan

		$umbracoOk = $false
		for ($i = 0; $i -lt 45; $i++) {
			try {
				$resp = Invoke-WebRequest -UseBasicParsing -Uri "https://localhost:$port/umbraco" -TimeoutSec 3 -ErrorAction Stop -SkipCertificateCheck
				if ($resp.StatusCode -ge 200 -and $resp.StatusCode -lt 400) {
					$umbracoOk = $true
					Write-Host "  /umbraco => HTTP $($resp.StatusCode) OK" -ForegroundColor Green
					break
				}
			}
			catch { }
			Start-Sleep -Seconds 2
		}

		if (-not $umbracoOk) {
			Write-Host "  FAILED: /umbraco not ready after 90s" -ForegroundColor Red
			Write-Host "--- Umbraco logs ---" -ForegroundColor Yellow
			docker logs $containerName 2>&1 | Select-Object -Last 60 | ForEach-Object { Write-Host $_ }
			return 2
		}

		if ($SkipSmoke.IsPresent) {
			Write-Host "  smoke.mjs tests SKIPPED (-SkipSmoke flag)" -ForegroundColor Yellow
		} elseif (-not $env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET) {
			Write-Host "  smoke.mjs tests SKIPPED (ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET not set)" -ForegroundColor Yellow
		} else {
			Write-Host "  Running smoke.mjs publish (publish Articulate content)..." -ForegroundColor Gray
			$env:UMBRACO_PUBLIC_URL = "https://localhost:$port/"
			$env:UMBRACO_RUNTIME_MODE = 'BackofficeDevelopment'
			$smokePublish = & node build/docker-site/smoke.mjs publish 2>&1
			if ($LASTEXITCODE -ne 0) {
				Write-Host "  smoke.mjs publish FAILED" -ForegroundColor Red
				$smokePublish | ForEach-Object { Write-Host "    $_" }
				return 2
			}
			Write-Host "  smoke.mjs publish OK" -ForegroundColor Green

			Write-Host "  Running smoke.mjs confirm (verify published)..." -ForegroundColor Gray
			$smokeConfirm = & node build/docker-site/smoke.mjs confirm 2>&1
			if ($LASTEXITCODE -ne 0) {
				Write-Host "  smoke.mjs confirm FAILED" -ForegroundColor Red
				$smokeConfirm | ForEach-Object { Write-Host "    $_" }
				return 2
			}
			Write-Host "  smoke.mjs confirm OK" -ForegroundColor Green
		}

		# Phase 2: Production mode - recreate container, test prod behavior
		Write-Host ""
		Write-Host "Phase 2: Production (recreate, smoke test, theme API)" -ForegroundColor Cyan
		Write-Host "────────────────────────────────────────────────────────────" -ForegroundColor Cyan

		Write-Host "  Recreating container in Production mode..." -ForegroundColor Gray
		docker rm -f $containerName 2>$null | Out-Null
		$prodEnvVars = $appEnvVars + @(
			'-e', 'Umbraco__CMS__Runtime__Mode=Production'
		)
		& docker run -d --rm --name $containerName --network $networkName @prodEnvVars $imgTag | Out-Null
		Start-Sleep -Seconds 5

		if ($SkipSmoke.IsPresent) {
			Write-Host "  Phase 2 smoke tests SKIPPED (-SkipSmoke flag)" -ForegroundColor Yellow
		} elseif (-not $env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET) {
			Write-Host "  Phase 2 smoke tests SKIPPED (ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET not set)" -ForegroundColor Yellow
		} else {
			Write-Host "  Running smoke.mjs smoke (verify / returns 200)..." -ForegroundColor Gray
			$env:UMBRACO_RUNTIME_MODE = 'Production'
			$smokeSmoke = & node build/docker-site/smoke.mjs smoke 2>&1
			if ($LASTEXITCODE -ne 0) {
				Write-Host "  smoke.mjs smoke FAILED" -ForegroundColor Red
				$smokeSmoke | ForEach-Object { Write-Host "    $_" }
				return 2
			}
			Write-Host "  smoke.mjs smoke OK" -ForegroundColor Green

			Write-Host "  Running smoke.mjs theme (verify theme API)..." -ForegroundColor Gray
			$smokeTheme = & node build/docker-site/smoke.mjs theme 2>&1
			if ($LASTEXITCODE -ne 0) {
				Write-Host "  smoke.mjs theme FAILED" -ForegroundColor Red
				$smokeTheme | ForEach-Object { Write-Host "    $_" }
				return 2
			}
			Write-Host "  smoke.mjs theme OK" -ForegroundColor Green
		}

		Write-Host ""
		if ($script:Keep) {
			Write-Host "  PASSED: $TargetName ($imgTag)  =>  https://localhost:$port/" -ForegroundColor Green
		} else {
			Write-Host "  PASSED: $TargetName ($imgTag)" -ForegroundColor Green
		}
		return 0
	}
	finally {
		if (-not $script:Keep) {
			docker rm -f $caddyName 2>$null | Out-Null
			docker rm -f $containerName 2>$null | Out-Null
			docker network rm $networkName 2>$null | Out-Null
		}
	}
}

# ── Main ──────────────────────────────────────────────────────────────────────

$targets = if ($Target -eq 'all') { @('umbraco17','umbraco18') } else { @($Target) }

# Build all needed package lanes up front
$lanes = $targets | ForEach-Object { $TargetConfigs[$_].PackageLane } | Sort-Object -Unique
foreach ($lane in $lanes) {
	Ensure-Packages -PackRoot $PackDir -Lane $lane
}

# Run each target
$exitCode = 0
$script:Keep = $Keep.IsPresent
foreach ($t in $targets) {
	$result = [int](Build-And-Test -TargetName $t -Config $TargetConfigs[$t])
	if ($result -ne 0 -and $exitCode -eq 0) {
		$exitCode = $result
	}
}

if ($Keep.IsPresent -and $exitCode -eq 0) {
	Write-Host ""
	Write-Host "Containers kept alive for manual inspection:" -ForegroundColor Cyan
	Write-Host "  docker ps --filter name=art-" -ForegroundColor White
	foreach ($t in $targets) {
		$port = $TargetConfigs[$t].ContainerPort
		Write-Host "  $t => https://localhost:$port/  |  https://localhost:$port/umbraco" -ForegroundColor Yellow
	}
	Write-Host ""
	Write-Host "To clean up:  docker rm -f $(docker ps -q --filter name=art-)" -ForegroundColor DarkGray
}

exit $exitCode
