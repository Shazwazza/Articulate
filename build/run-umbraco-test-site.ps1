#Requires -Version 5.1
<#
.SYNOPSIS
    Run Articulate.Tests.Website against one Umbraco version at a time.

.PARAMETER UmbracoVersion
    Umbraco major version to run against: 17 or 18.

.PARAMETER Configuration
    Build configuration passed to dotnet run. Defaults to Debug.

.PARAMETER ResetSiteData
    Remove the test site's local Umbraco data folder before launching.

.EXAMPLE
    .\build\run-umbraco-test-site.ps1 -UmbracoVersion 18 -Configuration Release
#>
[CmdletBinding()]
param(
    [ValidateSet(17, 18)]
    [int]$UmbracoVersion,

    [ValidateNotNullOrEmpty()]
    [string]$Configuration = 'Debug',

    [switch]$ResetSiteData
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectPath = Join-Path $repoRoot 'src/Articulate.Tests.Website/Articulate.Tests.Website.csproj'
$siteDataDir = Join-Path $repoRoot 'src/Articulate.Tests.Website/umbraco'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "Required command not found on PATH: dotnet"
}

$settings = switch ($UmbracoVersion) {
    17 {
        '17.4.0'
    }
    18 {
        '18.0.0-rc2'
    }
}

Write-Host "Running Articulate.Tests.Website for Umbraco $UmbracoVersion"
Write-Host "  Package   : $settings"
Write-Host "  Config    : $Configuration"

if ($ResetSiteData) {
    Write-Host "  Reset data: $siteDataDir"
    if (Test-Path $siteDataDir) {
        Remove-Item $siteDataDir -Recurse -Force
    }
}

& dotnet run -c $Configuration --project $projectPath "-p:ArticulatePackageLane=v$UmbracoVersion" "-p:UmbracoCmsPackageVersion=$settings"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet run failed for Umbraco $UmbracoVersion"
}
