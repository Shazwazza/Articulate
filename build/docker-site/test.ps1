<#
.SYNOPSIS
    Multi-lane Docker smoke test orchestrator.
    Delegates all container management to docker-compose.yml; no docker run.

.PARAMETER Target
    Lane(s) to test: umbraco17, umbraco18, or all (default).

.PARAMETER Keep
    Leave containers running after the test. Each lane uses a distinct port so both
    can stay alive simultaneously for manual inspection.

.PARAMETER SkipSmoke
    Skip smoke.mjs steps; only verify the container boots and Umbraco starts.

.EXAMPLE
    $env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET = 'articulate-dev-local-secret'
    .\build\docker-site\test.ps1
    .\build\docker-site\test.ps1 -Target umbraco18 -Keep
#>
param(
    [ValidateSet('umbraco17', 'umbraco18', 'all')]
    [string]$Target = 'all',
    [switch]$Keep,
    [switch]$SkipSmoke
)

$ErrorActionPreference = 'Stop'

$siteDir    = $PSScriptRoot
$repo       = Resolve-Path (Join-Path $siteDir '../..')
$devScript  = Join-Path $siteDir 'run-dev.ps1'
$prodScript = Join-Path $siteDir 'run-prod-smoke.ps1'

$laneCfg = [ordered]@{
    umbraco17 = @{ Lane='legacy';    TFM='net10.0'; UmbVer='[17.4.0,18.0.0)'; Tag='articulate-local:umbraco17'; HttpsPort='17017'; HttpPort='17080' }
    umbraco18 = @{ Lane='umbraco18'; TFM='net10.0'; UmbVer='[18.0.0-*,19.0.0)';       Tag='articulate-local:umbraco18'; HttpsPort='18018'; HttpPort='18080' }
}

$targets = if ($Target -eq 'all') { @('umbraco17', 'umbraco18') } else { @($Target) }

function Initialize-Packages([string]$lane) {
    $laneDir = Join-Path $repo "build/Release"
    $majorPrefix = if ($lane -eq 'umbraco18') { '7' } else { '6' }
    $pkg    = Get-ChildItem "$laneDir/Articulate.${majorPrefix}.*.nupkg" -ErrorAction SilentlyContinue | Select-Object -First 1
    $sample = Get-ChildItem "$laneDir/Articulate.Theme.Sample.${majorPrefix}.*.nupkg" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($pkg -and $sample) { return }
    Write-Host "Packages missing under $laneDir for lane '$lane' — building..."
    $env:ARTICULATE_PACKAGE_LANE = $lane
    $env:PACK_SAMPLE_THEME = 'true'
    & pwsh -NoLogo -File "$repo/build/build.ps1"
    if ($LASTEXITCODE -ne 0) { throw "build.ps1 failed for lane '$lane'" }
}

$passed = [System.Collections.Generic.List[string]]::new()
$failed = [System.Collections.Generic.List[string]]::new()

foreach ($t in $targets) {
    $cfg       = $laneCfg[$t]
    $hostPort  = "localhost:$($cfg.HttpsPort)"
    $publicUrl = "https://$hostPort/"

    Write-Host ""
    Write-Host "================================================================"
    Write-Host "  $t  |  Lane: $($cfg.Lane)  |  $($cfg.Tag)"
    Write-Host "  HTTPS: $publicUrl"
    Write-Host "================================================================"

    Initialize-Packages $cfg.Lane

    $env:COMPOSE_PROJECT_NAME           = "art_$t"
    $env:COMPOSE_VOLUME_PREFIX          = "art_$t"
    $env:IMAGE_TAG                      = $cfg.Tag
    $env:PACKAGE_SOURCE                 = "build/Release"
    $env:TARGET_FRAMEWORK               = $cfg.TFM
    $env:UMBRACO_CMS_VERSION            = $cfg.UmbVer
    $env:CADDY_HTTPS_PORT               = $cfg.HttpsPort
    $env:CADDY_HTTP_PORT                = $cfg.HttpPort
    $env:CADDY_HTTPS_HOST               = $hostPort
    $env:UMBRACO_PUBLIC_HOST            = "https://$hostPort"
    $env:UMBRACO_PUBLIC_URL             = $publicUrl
    $env:ARTICULATE_REDIRECT_URI        = "${publicUrl}a-new/"
    $env:ARTICULATE_LOGOUT_REDIRECT_URI = $publicUrl

    $ok = $true
    try {
        Push-Location $repo
        try {
            & docker compose build --no-cache --pull
            if ($LASTEXITCODE -ne 0) { throw "docker compose build failed" }

            $env:UMBRACO_RUNTIME_MODE = 'BackofficeDevelopment'
            if ($SkipSmoke) { & $devScript -SkipPublish } else { & $devScript }

            if (-not $SkipSmoke) {
                $env:UMBRACO_RUNTIME_MODE = 'Production'
                & $prodScript
            }
        }
        finally { Pop-Location }

        $passed.Add($t)
        Write-Host "PASSED: $t => $publicUrl"
    }
    catch {
        $ok = $false
        $failed.Add($t)
        Write-Warning "FAILED: $t — $_"
    }
    finally {
        if (-not $Keep -or -not $ok) {
            Push-Location $repo
            & docker compose down -v
            Pop-Location
        }
    }
}

if ($Keep -and $passed.Count -gt 0) {
    Write-Host ""
    Write-Host "Containers kept alive for inspection:"
    foreach ($t in $passed) {
        $cfg = $laneCfg[$t]
        Write-Host "  $t => https://localhost:$($cfg.HttpsPort)/  |  https://localhost:$($cfg.HttpsPort)/umbraco"
    }
    Write-Host "To clean up: docker ps --filter name=art_ -q | ForEach-Object { docker rm -f `$_ }"
}

if ($failed.Count -gt 0) { exit 1 }
