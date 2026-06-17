#Requires -Version 5.1
<#
.SYNOPSIS
    Check the current Articulate BackOffice client for one Umbraco version at a time.

.PARAMETER UmbracoVersion
    Umbraco major version to check against: 17 or 18.

.EXAMPLE
    .\build\check-backoffice-client.ps1 -UmbracoVersion 18
#>
[CmdletBinding()]
param(
    [ValidateSet(17, 18)]
    [int]$UmbracoVersion
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$clientRoot = Join-Path $repoRoot 'src/Articulate.Web/Client'
$clientDir = Join-Path $clientRoot "v$UmbracoVersion"

if (-not (Get-Command pnpm -ErrorAction SilentlyContinue)) {
    throw "Required command not found on PATH: pnpm"
}

Write-Host "Checking Articulate BackOffice client for Umbraco $UmbracoVersion"
Write-Host "  Client   : $clientDir"

Push-Location $clientRoot
try {
    & pnpm install
    if ($LASTEXITCODE -ne 0) { throw "pnpm install failed" }

    Set-Location $clientDir
    & pnpm run check
    if ($LASTEXITCODE -ne 0) { throw "pnpm run check failed" }

    & pnpm run build
    if ($LASTEXITCODE -ne 0) { throw "pnpm run build failed" }
}
finally {
    Pop-Location
}
