param(
    [ValidateSet("legacy", "umbraco18")]
    [string] $Lane = "legacy",

    [ValidateSet("build", "run", "up", "compose-build", "compose-up", "compose-down")]
    [string] $Action = "up",

    [string] $Configuration = $(if ([string]::IsNullOrWhiteSpace($env:BUILD_CONFIGURATION)) { "Release" } else { $env:BUILD_CONFIGURATION })
)

$ErrorActionPreference = "Stop"
$ScriptStart = Get-Date
$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
$RepoRoot = $PSScriptFilePath.Directory.Parent.FullName

if ($Lane -eq "umbraco18") {
    $umbracoVersion = "[18.0.0-*,19.0.0)"
    $imageTag = "articulate-local:umbraco18"
    $containerName = "articulate-umbraco18"
    $port = 18018
    $email = "admin18@localhost"
}
else {
    $umbracoVersion = "[17.4.0,18.0.0)"
    $imageTag = "articulate-local:umbraco17"
    $containerName = "articulate-umbraco17"
    $port = 18017
    $email = "admin17@localhost"
}

$packageSource = "build/$Configuration/$Lane"
$packagePath = Join-Path $RepoRoot $packageSource
$requiresPackage = $Action -ne "compose-down"
if ($requiresPackage) {
    if (-not (Test-Path -LiteralPath $packagePath)) {
        throw "Package lane folder '$packagePath' does not exist. Run build/build.ps1 with ARTICULATE_PACKAGE_LANE=$Lane first."
    }
    if (-not (Get-ChildItem -LiteralPath $packagePath -Filter "Articulate.*.nupkg" | Where-Object { $_.Name -notlike "*.snupkg" })) {
        throw "Package lane folder '$packagePath' does not contain an Articulate .nupkg."
    }
    if (-not (Get-ChildItem -LiteralPath $packagePath -Filter "Articulate.Theme.Sample.*.nupkg")) {
        throw "Package lane folder '$packagePath' does not contain Articulate.Theme.Sample. Rebuild with PACK_SAMPLE_THEME=true."
    }
}

function Build-Image {
    Write-Host "Building $imageTag from $packageSource with Umbraco $umbracoVersion"
    & docker build `
        --build-arg "UMBRACO_CMS_VERSION=$umbracoVersion" `
        --build-arg "BUILD_CONFIGURATION=$Configuration" `
        --build-arg "PACKAGE_SOURCE=$packageSource" `
        -t $imageTag `
        $RepoRoot
    if ($LASTEXITCODE -ne 0) { throw "docker build failed for $Lane" }
}

function Run-Container {
    Write-Host "Starting $containerName on http://localhost:$port/umbraco"
    Write-Host "Direct HTTP mode is a boot/package smoke test. Use -Action compose-up for backoffice auth over HTTPS."
    & docker rm -f $containerName 2>$null | Out-Null
    & docker run -d `
        --name $containerName `
        -p "${port}:8080" `
        -e "ASPNETCORE_ENVIRONMENT=Container" `
        -e "ASPNETCORE_URLS=http://+:8080" `
        -e "ConnectionStrings__umbracoDbDSN=Data Source=/app/umbraco/Data/Umbraco.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True" `
        -e "ConnectionStrings__umbracoDbDSN_ProviderName=Microsoft.Data.Sqlite" `
        -e "Umbraco__CMS__WebRouting__UmbracoApplicationUrl=http://localhost:$port/" `
        -e "Umbraco__CMS__Security__BackOfficeHost=http://localhost:$port" `
        -e "Umbraco__CMS__Unattended__InstallUnattended=true" `
        -e "Umbraco__CMS__Unattended__UpgradeUnattended=true" `
        -e "Umbraco__CMS__Unattended__UnattendedUserName=Jane Doe" `
        -e "Umbraco__CMS__Unattended__UnattendedUserEmail=$email" `
        -e "Umbraco__CMS__Unattended__UnattendedUserPassword=@rticulate" `
        -e "Umbraco__CMS__ModelsBuilder__ModelsMode=Nothing" `
        $imageTag
    if ($LASTEXITCODE -ne 0) { throw "docker run failed for $Lane" }
}

function Invoke-Compose {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("build", "up", "down")]
        [string] $ComposeAction
    )

    $projectName = "articulate-$Lane"
    $volumePrefix = "articulate_$Lane"
    $oldValues = @{
        BUILD_CONFIGURATION = $env:BUILD_CONFIGURATION
        PACKAGE_SOURCE = $env:PACKAGE_SOURCE
        UMBRACO_CMS_VERSION = $env:UMBRACO_CMS_VERSION
        IMAGE_TAG = $env:IMAGE_TAG
        UMBRACO_USER_EMAIL = $env:UMBRACO_USER_EMAIL
        COMPOSE_VOLUME_PREFIX = $env:COMPOSE_VOLUME_PREFIX
        UMBRACO_PUBLIC_HOST = $env:UMBRACO_PUBLIC_HOST
        UMBRACO_PUBLIC_URL = $env:UMBRACO_PUBLIC_URL
        ARTICULATE_REDIRECT_URI = $env:ARTICULATE_REDIRECT_URI
        ARTICULATE_LOGOUT_REDIRECT_URI = $env:ARTICULATE_LOGOUT_REDIRECT_URI
    }

    try {
        $env:BUILD_CONFIGURATION = $Configuration
        $env:PACKAGE_SOURCE = $packageSource
        $env:UMBRACO_CMS_VERSION = $umbracoVersion
        $env:IMAGE_TAG = $imageTag
        $env:UMBRACO_USER_EMAIL = $email
        $env:COMPOSE_VOLUME_PREFIX = $volumePrefix
        $env:UMBRACO_PUBLIC_HOST = "https://localhost:18443"
        $env:UMBRACO_PUBLIC_URL = "https://localhost:18443/"
        $env:ARTICULATE_REDIRECT_URI = "https://localhost:18443/a-new/"
        $env:ARTICULATE_LOGOUT_REDIRECT_URI = "https://localhost:18443/"

        switch ($ComposeAction) {
            "build" {
                Write-Host "Building Compose HTTPS lane $Lane from $packageSource"
                & docker compose -p $projectName build articulate
            }
            "up" {
                Write-Host "Starting Compose HTTPS lane $Lane at https://localhost:18443/umbraco"
                Write-Host "Only one Compose HTTPS lane can bind port 18443 at a time."
                & docker compose -p $projectName up -d --build --force-recreate
            }
            "down" {
                Write-Host "Stopping Compose HTTPS lane $Lane"
                & docker compose -p $projectName down
            }
        }
        if ($LASTEXITCODE -ne 0) { throw "docker compose $ComposeAction failed for $Lane" }
    }
    finally {
        foreach ($key in $oldValues.Keys) {
            if ($null -eq $oldValues[$key]) {
                Remove-Item "Env:$key" -ErrorAction SilentlyContinue
            }
            else {
                Set-Item "Env:$key" $oldValues[$key]
            }
        }
    }
}

switch ($Action) {
    "build" { Build-Image }
    "run" { Run-Container }
    "up" {
        Build-Image
        Run-Container
    }
    "compose-build" { Invoke-Compose -ComposeAction build }
    "compose-up" { Invoke-Compose -ComposeAction up }
    "compose-down" { Invoke-Compose -ComposeAction down }
}

$elapsed = (Get-Date) - $ScriptStart
Write-Host ("Docker lane '{0}' {1} completed in {2:N1}s." -f $Lane, $Action, $elapsed.TotalSeconds)
