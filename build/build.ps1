# Usage:
#   BUILD_CONFIGURATION=Debug pwsh -NoLogo -File build/build.ps1
#   ENABLE_CLIENT_BUILD=true pwsh -NoLogo -File build/build.ps1
#   RUN_TESTS=true pwsh -NoLogo -File build/build.ps1
#   PACK_SAMPLE_THEME=true pwsh -NoLogo -File build/build.ps1
#   ARTICULATE_PACKAGE_LANE=umbraco18 ARTICULATE_PACKAGE_VERSION=7.0.0-rc.1 pwsh -NoLogo -File build/build.ps1
# Release builds enable the client build by default so packaged assets carry the stamped version.
$ScriptStart = Get-Date
$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
$RepoRoot = $PSScriptFilePath.Directory.Parent.FullName
$BuildFolder = Join-Path -Path $RepoRoot -ChildPath "build"
$Configuration = if ([string]::IsNullOrWhiteSpace($env:BUILD_CONFIGURATION)) { "Release" } else { $env:BUILD_CONFIGURATION }
$ReleaseRoot = Join-Path -Path $BuildFolder -ChildPath $Configuration
$PackageLane = if ([string]::IsNullOrWhiteSpace($env:ARTICULATE_PACKAGE_LANE)) { "legacy" } else { $env:ARTICULATE_PACKAGE_LANE.ToLowerInvariant() }
if ($PackageLane -notin @("legacy", "umbraco18")) {
    throw "Unsupported ARTICULATE_PACKAGE_LANE '$PackageLane'. Expected 'legacy' or 'umbraco18'."
}
$ReleaseFolder = Join-Path -Path $ReleaseRoot -ChildPath $PackageLane
$SolutionRoot = Join-Path -Path $RepoRoot -ChildPath "src"
$SolutionPath = Join-Path -Path $SolutionRoot -ChildPath "Articulate.sln"
$TargetFrameworks = if ($PackageLane -eq "umbraco18") { @("net10.0") } else { @("net9.0", "net10.0") }
$PSMajorVersion = $PSVersionTable.PSVersion.Major
$SupportsParallel = $PSMajorVersion -ge 7
if (-not $SupportsParallel) {
    Write-Warning "PowerShell $PSMajorVersion detected; ForEach-Object -Parallel isn't available so build + pack will run sequentially."
}

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
$msbuildArgs = @("-m", "-maxcpucount:$cpu", "-p:BuildInParallel=true", "-p:RestoreUseStaticGraphEvaluation=true")
if ($PackageLane -eq "umbraco18") {
    $msbuildArgs = @("-m", "-maxcpucount:$cpu", "-p:BuildInParallel=true")
}
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
$laneProperties = @("-p:ArticulatePackageLane=$PackageLane")
if ($PackageLane -eq "umbraco18") {
    $laneProperties += @(
        "-p:TargetFramework=net10.0",
        "-p:UmbracoCmsPackageVersion=18.0.0-*"
    )
}
if (-not [string]::IsNullOrWhiteSpace($env:ARTICULATE_PACKAGE_VERSION)) {
    $laneProperties += @(
        "-p:Version=$env:ARTICULATE_PACKAGE_VERSION",
        "-p:PackageVersion=$env:ARTICULATE_PACKAGE_VERSION"
    )
}
$packProperties = @($laneProperties)
if ($PackageLane -eq "umbraco18") {
    $packProperties += "-p:TargetFrameworks=net10.0"
}
$packSampleTheme = ($env:PACK_SAMPLE_THEME -eq 'true') -or ([string]::IsNullOrEmpty($env:PACK_SAMPLE_THEME) -and -not $runningInCi)
$dotnetCommon = @("-v", "minimal")
Write-Host "Using up to $cpu parallel MSBuild nodes"
Write-Host "Build configuration: $Configuration"
Write-Host "Package lane: $PackageLane"
Write-Host "Package output: $ReleaseFolder"

$script:versionJsonPath = Join-Path -Path $RepoRoot -ChildPath "version.json"
$script:originalVersionJson = $null
function Restore-VersionJson {
    if ($null -ne $script:originalVersionJson) {
        Set-Content -LiteralPath $script:versionJsonPath -Value $script:originalVersionJson -NoNewline
    }
}
trap {
    Restore-VersionJson
    break
}

if (-not [string]::IsNullOrWhiteSpace($env:ARTICULATE_PACKAGE_VERSION)) {
    $script:originalVersionJson = Get-Content -LiteralPath $script:versionJsonPath -Raw
    $updatedVersionJson = $script:originalVersionJson -replace '("version"\s*:\s*")[^"]+(")', "`${1}$env:ARTICULATE_PACKAGE_VERSION`${2}"
    Set-Content -LiteralPath $script:versionJsonPath -Value $updatedVersionJson -NoNewline
    Write-Host "Temporarily using package version: $env:ARTICULATE_PACKAGE_VERSION"
}

# Friendly note if running on Windows against a WSL filesystem (\\wsl$ UNC)
if ($RepoRoot.StartsWith("\\\\wsl$", [System.StringComparison]::OrdinalIgnoreCase) -or 
    $RepoRoot.StartsWith("\\\\wsl.localhost\\", [System.StringComparison]::OrdinalIgnoreCase)) {
    Write-Warning "Windows->WSL performance tip: Repo is under '$RepoRoot'. Builds run faster inside the WSL distro. Prefer running bash build/build.sh from WSL ext4."
}
if (Test-Path $ReleaseFolder) {
    Write-Warning "$ReleaseFolder already exists on your local machine. It will now be deleted."
    Remove-Item $ReleaseFolder -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $ReleaseFolder | Out-Null

dotnet --version

# 1) Clean the solution to ensure release/CI builds start from a fresh slate
Write-Host "1. Cleaning solution outputs..."
if ($env:FORCE_CLEAN -eq 'true') {
    & dotnet clean $SolutionPath -c $Configuration @dotnetCommon $clientBuildProperty @laneProperties
    if ($LASTEXITCODE -ne 0) { Write-Host "Warning dotnet clean failed" }
}
else {
    Write-Host "Skipping dotnet clean (set FORCE_CLEAN=true to force a clean)"
}

# 2) Restore (solution-level)
Write-Host "2. Restoring solution packages in parallel..."
& dotnet restore $SolutionPath @dotnetCommon @msbuildArgs $clientBuildProperty @laneProperties
if (-not $?) { throw "dotnet restore failed" }

# 3) Build TFMs sequentially to ensure net9.0 (client build) runs before net10.0
Write-Host "3. Building solution for: $($TargetFrameworks -join ', ')"
foreach ($tfm in $TargetFrameworks) {
    Write-Host "[build] -> $tfm"
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    & dotnet build $SolutionPath -c $Configuration -f $tfm --no-restore @dotnetCommon @msbuildArgs $clientBuildProperty @laneProperties
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed for $tfm" }
    $sw.Stop()
    Write-Host "[build] <- $tfm done in $([int]$sw.Elapsed.TotalSeconds)s"
}

# 4) Run tests
if ($runTests) {
    Write-Host "4. Running tests..."
    & dotnet test $SolutionPath -c $Configuration --no-restore --no-build @dotnetCommon @laneProperties
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
$packThrottle = 1
if ($SupportsParallel) {
    $projectsToPack | ForEach-Object -Parallel {
        $project = $PSItem
        Write-Host "[pack] -> $([IO.Path]::GetFileName($project))"
        $restoreArgs = @("--no-restore")
        $commonArgs = $using:dotnetCommon
        $clientBuildSwitch = $using:clientBuildProperty
        $laneSwitches = $using:packProperties
        & dotnet pack -c $using:Configuration $project @restoreArgs -o $using:ReleaseFolder @commonArgs "-p:BuildInParallel=false" $clientBuildSwitch @laneSwitches
        if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed for $project" }
    } -ThrottleLimit $packThrottle -ErrorVariable packErrors
    if ($packErrors) { throw "One or more pack operations failed: $($packErrors | Out-String)" }
}
else {
    foreach ($project in $projectsToPack) {
        Write-Host "[pack] -> $([IO.Path]::GetFileName($project))"
        & dotnet pack -c $Configuration $project --no-restore -o $ReleaseFolder @dotnetCommon "-p:BuildInParallel=false" $clientBuildProperty @packProperties
        if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed for $project" }
    }
}
$TotalSeconds = (Get-Date) - $ScriptStart
Restore-VersionJson
Write-Host ("Build pipeline completed in {0:N1}s. Packages available at {1}" -f $TotalSeconds.TotalSeconds, $ReleaseFolder)
