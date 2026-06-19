#Requires -Version 5.1
<#
.SYNOPSIS
    Production smoke wrapper for Windows (PowerShell).
    Starts the Docker stack in Production mode with --force-recreate, then runs smoke.mjs
    smoke + theme to verify published content and a management API theme switch.

.EXAMPLE
    .\build\docker-site\run-prod-smoke.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'docker.common.ps1')

# --- Validate prerequisites ---
if (-not (Test-Command 'docker')) {
    throw "Required command not found on PATH: docker"
}

$nodeBin = Resolve-Node

$projectDir = Resolve-Path (Join-Path $PSScriptRoot '..' '..')
Push-Location $projectDir

try {
    $env:UMBRACO_RUNTIME_MODE = if ($env:UMBRACO_RUNTIME_MODE) { $env:UMBRACO_RUNTIME_MODE } else { 'Production' }
    $env:UMBRACO_PUBLIC_HOST = if ($env:UMBRACO_PUBLIC_HOST) { $env:UMBRACO_PUBLIC_HOST } else { 'https://localhost:18443' }
    $env:UMBRACO_PUBLIC_URL = if ($env:UMBRACO_PUBLIC_URL) { $env:UMBRACO_PUBLIC_URL } else { 'https://localhost:18443/' }

    if (-not $env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET) {
        throw "ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET must be set."
    }
    $env:ARTICULATE_DEV_AUTOMATION_CLIENT_ID = if ($env:ARTICULATE_DEV_AUTOMATION_CLIENT_ID) { $env:ARTICULATE_DEV_AUTOMATION_CLIENT_ID } else { 'articulate-dev-automation' }

    & docker compose up -d --force-recreate

    $smokeScript = Join-Path $projectDir 'build' 'docker-site' 'smoke.mjs'
    & $nodeBin $smokeScript smoke
    & $nodeBin $smokeScript theme
}
finally {
    Pop-Location
}
