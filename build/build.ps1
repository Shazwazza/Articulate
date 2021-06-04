param (
	[Parameter(Mandatory=$true)]
	[ValidatePattern("^\d\.\d\.(?:\d\.\d$|\d$)")]
	[string]
	$ReleaseVersionNumber,
	[Parameter(Mandatory=$true)]
	[string]
	[AllowEmptyString()]
	$PreReleaseName
)

$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
$RepoRoot = $PSScriptFilePath.Directory.Parent.FullName
$BuildFolder = Join-Path -Path $RepoRoot -ChildPath "build";
$ReleaseFolder = Join-Path -Path $BuildFolder -ChildPath "Release";
$PackageFilePath = Join-Path -Path $BuildFolder -ChildPath "package.xml"
$SolutionRoot = Join-Path -Path $RepoRoot "src";

# Ensure umbPack is installed
$IsUmbPackInstalled = dotnet tool update -g Umbraco.Tools.Packages

if(-not $IsUmbPackInstalled){
	dotnet tool install -g Umbraco.Tools.Packages
}

if ((Get-Item $ReleaseFolder -ErrorAction SilentlyContinue) -ne $null)
{
	Write-Warning "$ReleaseFolder already exists on your local machine. It will now be deleted."
	Remove-Item $ReleaseFolder -Recurse
}

####### DO THE UMBRACO PACKAGE BUILD #############

# Set the version number in package.xml
$PackageConfig = Join-Path -Path $BuildFolder -ChildPath "package.xml"
$PackageConfigXML = [xml](Get-Content $PackageConfig)
$PackageConfigXML.umbPackage.info.package.version = "$ReleaseVersionNumber"
$PackageConfigXML.Save($PackageConfig)

umbpack pack $PackageFilePath -o $ReleaseFolder -v ($ReleaseVersionNumber + $PreReleaseName)