# Usage:
#   BUILD_CONFIGURATION=Debug pwsh -NoLogo -File build/build.ps1
#   ENABLE_CLIENT_BUILD=true pwsh -NoLogo -File build/build.ps1
#   RUN_TESTS=true pwsh -NoLogo -File build/build.ps1
#   PACK_SAMPLE_THEME=true pwsh -NoLogo -File build/build.ps1
#   ARTICULATE_PACKAGE_LANE=v18 pwsh -NoLogo -File build/build.ps1
# Set SKIP_CLEAN=true only when deliberately reusing outputs from the same lane.
# Release builds enable the client build by default so packaged assets carry the stamped version.
$ScriptStart = Get-Date
$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
$RepoRoot = $PSScriptFilePath.Directory.Parent.FullName
$BuildFolder = Join-Path -Path $RepoRoot -ChildPath "build"
$V18VersionFile = Join-Path $BuildFolder "v18-version.txt"
$Configuration = if ([string]::IsNullOrWhiteSpace($env:BUILD_CONFIGURATION)) { "Release" } else { $env:BUILD_CONFIGURATION }
$ReleaseRoot = Join-Path -Path $BuildFolder -ChildPath $Configuration
$PackageLane = if ([string]::IsNullOrWhiteSpace($env:ARTICULATE_PACKAGE_LANE)) { "v17" } else { $env:ARTICULATE_PACKAGE_LANE.ToLowerInvariant() }
if ($PackageLane -notin @("v17", "v18")) {
    throw "Unsupported ARTICULATE_PACKAGE_LANE '$PackageLane'. Expected 'v17' or 'v18'."
}
$ReleaseFolder = Join-Path $ReleaseRoot $PackageLane
$SolutionRoot = Join-Path -Path $RepoRoot -ChildPath "src"
$SolutionPath = Join-Path -Path $SolutionRoot -ChildPath "Articulate.sln"
$BackofficeOutput = Join-Path $SolutionRoot "Articulate.Web/wwwroot/App_Plugins/Articulate/BackOffice"
# Ensure dotnet is discoverable when installed under the user profile
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $userProfile = if ($HOME) { $HOME } else { $env:USERPROFILE }
    if ($userProfile) {
        $userDotnet = Join-Path $userProfile ".dotnet"
        $dotnetExe = Join-Path $userDotnet "dotnet.exe"
        if (Test-Path $dotnetExe) {
            $env:PATH = "$env:PATH;$userDotnet;$userDotnet\tools"
        }
    }
}

# Performance-friendly env
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_NOLOGO = "1"
$env:NUGET_XMLDOC_MODE = "none"
$env:RestoreFallbackFolders = ""

# Compute CPU parallelism for MSBuild
$cpu = [Environment]::ProcessorCount
if ($env:MAXCPU -and ($env:MAXCPU -as [int]) -gt 0) {
    $cpu = [int]$env:MAXCPU
}
$restoreArgs = @("-m", "-maxcpucount:$cpu", "-p:RestoreUseStaticGraphEvaluation=true")
$buildArgs = @("-m:1", "-p:BuildInParallel=false", "-p:UseSharedCompilation=false")
$runningInCi = ($env:CI -eq 'true') -or ($env:GITHUB_ACTIONS -eq 'true')
if ([string]::IsNullOrEmpty($env:RUN_TESTS)) {
    $runTests = $runningInCi
}
else {
    $runTests = $env:RUN_TESTS -eq 'true'
}
if ([string]::IsNullOrEmpty($env:ENABLE_CLIENT_BUILD)) {
    $clientBuildValue = if ($runningInCi -or $Configuration -eq 'Release') { 'true' } else { 'false' }
}
else {
    $clientBuildValue = $env:ENABLE_CLIENT_BUILD
}
$clientBuildProperty = "-p:EnableClientBuild=$clientBuildValue"

$resolvedClientVersion = if ($PackageLane -eq "v18") { "18" } else { "17" }
$packageVersion = $env:ARTICULATE_PACKAGE_VERSION
if ($PackageLane -eq "v18" -and [string]::IsNullOrWhiteSpace($packageVersion)) {
    $v18BaseVersion = (Get-Content -LiteralPath $V18VersionFile -Raw).Trim()
    if ($v18BaseVersion -notmatch '^7\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
        throw "Invalid v18 base version '$v18BaseVersion' in $V18VersionFile."
    }
    if (-not (Get-Command nbgv -ErrorAction SilentlyContinue)) {
        throw "The v18 lane requires ARTICULATE_PACKAGE_VERSION or the nbgv CLI."
    }
    $v17Version = (& nbgv get-version -v SemVer2).Trim()
    if ($LASTEXITCODE -ne 0 -or $v17Version -notmatch '^6\.1\.\d+(?:-(.+))?$') {
        throw "Expected NBGV to produce a 6.1.x version, got '$v17Version'."
    }
    $packageVersion = $v18BaseVersion
    if ($Matches[1]) {
        $packageVersion += ".$($Matches[1])"
    }
}
$laneProperties = @(
    "-p:ArticulatePackageLane=$PackageLane",
    "-p:UmbracoClientVersion=$resolvedClientVersion"
)
if (-not [string]::IsNullOrWhiteSpace($packageVersion)) {
    $laneProperties += "-p:ArticulatePackageVersion=$packageVersion"
}
$packSampleTheme = ($env:PACK_SAMPLE_THEME -eq 'true') -or ([string]::IsNullOrEmpty($env:PACK_SAMPLE_THEME) -and -not $runningInCi)
$dotnetCommon = @("-v", "minimal")
Write-Host "Restore parallelism: up to $cpu MSBuild nodes"
Write-Host "Build configuration: $Configuration"
Write-Host "Package lane: $PackageLane"
Write-Host "Package version: $(if ($packageVersion) { $packageVersion } else { 'NBGV' })"
Write-Host "Package output: $ReleaseFolder"
# Remove only packages from the same major version line to avoid stale artifacts without
# wiping the other lane's output when both are built locally.
$majorVersionPrefix = if ($PackageLane -eq "v18") { "7" } else { "6" }
if (Test-Path $ReleaseFolder) {
    Get-ChildItem -Path $ReleaseFolder -Include "Articulate.${majorVersionPrefix}.*.nupkg", "Articulate.${majorVersionPrefix}.*.snupkg", "Articulate.Theme.Sample.${majorVersionPrefix}.*.nupkg" | Remove-Item -Force
}
else {
    New-Item -ItemType Directory -Force -Path $ReleaseFolder | Out-Null
}

# Friendly note if running on Windows against a WSL filesystem (\\wsl$ UNC)
if ($RepoRoot.StartsWith("\\\\wsl$", [System.StringComparison]::OrdinalIgnoreCase) -or
    $RepoRoot.StartsWith("\\\\wsl.localhost\\", [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Warning "Windows->WSL performance tip: Repo is under '$RepoRoot'. Builds run faster inside the WSL distro. Prefer running bash build/build.sh from WSL ext4."
}

dotnet --version

# 1) Clean outputs. This is required between lanes because both clients write to the same static asset path.
Write-Host "1. Cleaning solution outputs..."
if ($env:SKIP_CLEAN -ne 'true') {
    & dotnet build-server shutdown | Out-Null
    Get-ChildItem -Path $SolutionRoot -Directory -Recurse -Force |
        Where-Object { $_.Name -in @('bin', 'obj') -and $_.FullName -notmatch '\\node_modules\\' } |
        ForEach-Object { Remove-Item $_.FullName -Recurse -Force }
    Remove-Item -LiteralPath (Join-Path $BuildFolder 'ClientAssets') -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $BackofficeOutput -Recurse -Force -ErrorAction SilentlyContinue
}
else {
    Write-Host "Skipping clean because SKIP_CLEAN=true"
}

# 2) Restore (solution-level)
Write-Host "2. Restoring solution packages in parallel..."
& dotnet restore $SolutionPath @dotnetCommon @restoreArgs $clientBuildProperty @laneProperties
if (-not $?) { throw "dotnet restore failed" }

# 3) Build sequentially because the solution references shared projects through multiple paths.
Write-Host "3. Building solution for net10.0"
& dotnet build $SolutionPath -c $Configuration --no-restore @dotnetCommon @buildArgs $clientBuildProperty @laneProperties
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

# 4) Run tests
if ($runTests) {
    Write-Host "4. Running tests..."
    & dotnet test $SolutionPath -c $Configuration --no-restore --no-build @dotnetCommon @buildArgs @laneProperties
    if ($LASTEXITCODE -ne 0) { throw "dotnet test failed" }
}
else {
    Write-Host "4. Skipping tests (set RUN_TESTS=true to enable locally)"
}

# 5) Pack primary projects
Write-Host "5. Packing projects..."
$articulateWebProject = (Join-Path $SolutionRoot 'Articulate.Web/Articulate.Web.csproj')
$articulateThemeSampleProject = (Join-Path $SolutionRoot 'Articulate.Theme.Sample/Articulate.Theme.Sample.csproj')
$projectsToPack = @(
    $articulateWebProject
)
if ($packSampleTheme) {
    $projectsToPack += $articulateThemeSampleProject
}
foreach ($project in $projectsToPack) {
    Write-Host "[pack] -> $([IO.Path]::GetFileName($project))"
    & dotnet pack -c $Configuration $project --no-restore --no-build -o $ReleaseFolder @dotnetCommon "-m:1" "-p:BuildInParallel=false" "-p:UseSharedCompilation=false" $clientBuildProperty @laneProperties
    if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed for $project" }
}
$TotalSeconds = (Get-Date) - $ScriptStart
Write-Host ("Build pipeline completed in {0:N1}s. Packages available at {1}" -f $TotalSeconds.TotalSeconds, $ReleaseFolder)
