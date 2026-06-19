#Requires -Version 5.1
<#
.SYNOPSIS
    Dev smoke wrapper for Windows (PowerShell).
    Starts the Docker stack in BackofficeDevelopment mode, waits for Umbraco to be ready,
    then runs smoke.mjs publish + confirm to publish and verify Articulate content via the Management API.

.PARAMETER ResetDockerVolumes
    Runs 'docker compose down -v' before starting so the install begins from an empty DB and media volume.

.PARAMETER SkipPublish
    Skips the smoke.mjs publish/confirm step if you only want the container boot.

.EXAMPLE
    $env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET = "..."
    .\build\docker-site\run-dev.ps1

.EXAMPLE
    $env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET = "..."
    .\build\docker-site\run-dev.ps1 -ResetDockerVolumes
#>
[CmdletBinding()]
param(
    [switch]$ResetDockerVolumes,
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'docker.common.ps1')

function Wait-ForUrl {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 300
    )

    $curlTlsArgs = @()
    if ($Url -match '^https://(localhost|127\.0\.0\.1|\[::1\])') {
        $curlTlsArgs = @('-k')
    }

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $code = (& curl.exe @curlTlsArgs -sS -o $null -w '%{http_code}' $Url 2>$null) | Out-String
            $code = $code.Trim()
            if ($code -eq '200' -or $code -eq '302') {
                return
            }
        }
        catch {
            # ignore
        }
        Start-Sleep -Seconds 2
    }

    throw "Timed out waiting for $Url to become reachable."
}

function Wait-ForUmbracoReady {
    $publicBase = $env:UMBRACO_PUBLIC_URL
    if (-not $publicBase) {
        $publicBase = 'https://localhost:18443/'
    }
    $publicBase = $publicBase.TrimEnd('/')
    Wait-ForUrl -Url "$publicBase/umbraco"
}

# --- Validate prerequisites ---
if (-not (Test-Command 'docker')) {
    throw "Required command not found on PATH: docker"
}
if (-not (Test-Command 'curl')) {
    throw "Required command not found on PATH: curl"
}

$projectDir = Resolve-Path (Join-Path $PSScriptRoot '..' '..')
Push-Location $projectDir

try {
    $env:UMBRACO_RUNTIME_MODE = if ($env:UMBRACO_RUNTIME_MODE) { $env:UMBRACO_RUNTIME_MODE } else { 'BackofficeDevelopment' }

    if (-not $SkipPublish) {
        $nodeBin = Resolve-Node

        if (-not $env:ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET) {
            throw "ARTICULATE_DEV_AUTOMATION_CLIENT_SECRET must be set."
        }

        $env:ARTICULATE_DEV_AUTOMATION_CLIENT_ID = if ($env:ARTICULATE_DEV_AUTOMATION_CLIENT_ID) { $env:ARTICULATE_DEV_AUTOMATION_CLIENT_ID } else { 'articulate-dev-automation' }
        $env:UMBRACO_PUBLIC_URL = if ($env:UMBRACO_PUBLIC_URL) { $env:UMBRACO_PUBLIC_URL } else { 'https://localhost:18443/' }
    }

    if ($ResetDockerVolumes) {
        Write-Host "Resetting Docker containers and volumes before dev run."
        & docker compose down -v
    }

    & docker compose up -d

    $serviceId = & docker compose ps -q articulate | Out-String
    if (-not $serviceId.Trim()) {
        throw "Could not find the articulate service container id."
    }

    Wait-ForUmbracoReady

    $smokeScript = Join-Path $projectDir 'build' 'docker-site' 'smoke.mjs'
    if (-not $SkipPublish) {
        & $nodeBin $smokeScript publish
        & $nodeBin $smokeScript confirm
    }
}
finally {
    Pop-Location
}
