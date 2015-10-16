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
$WebProjFolder = Join-Path -Path $RepoRoot -ChildPath "src\Articulate.Web";
$ReleaseFolder = Join-Path -Path $BuildFolder -ChildPath "Releases\v$ReleaseVersionNumber$PreReleaseName";
$TempFolder = Join-Path -Path $ReleaseFolder -ChildPath "Temp";
$SolutionRoot = Join-Path -Path $RepoRoot "src";
$MSBuild = "${Env:ProgramFiles(x86)}\MSBuild\14.0\Bin\MsBuild.exe";


if ((Get-Item $ReleaseFolder -ErrorAction SilentlyContinue) -ne $null)
{
	Write-Warning "$ReleaseFolder already exists on your local machine. It will now be deleted."
	Remove-Item $ReleaseFolder -Recurse
}

####### DO THE SLN BUILD PART #############

# Go get nuget.exe if we don't hae it
$NuGet = "$BuildFolder\nuget.exe"
$FileExists = Test-Path $NuGet 
If ($FileExists -eq $False) {
	$SourceNugetExe = "http://nuget.org/nuget.exe"
	Invoke-WebRequest $SourceNugetExe -OutFile $NuGet
}

# Set the version number in SolutionInfo.cs
$SolutionInfoPath = Join-Path -Path $SolutionRoot -ChildPath "SolutionInfo.cs"
(gc -Path $SolutionInfoPath) `
	-replace "(?<=Version\(`")[.\d]*(?=`"\))", $ReleaseVersionNumber |
	sc -Path $SolutionInfoPath -Encoding UTF8
(gc -Path $SolutionInfoPath) `
	-replace "(?<=AssemblyInformationalVersion\(`")[.\w-]*(?=`"\))", "$ReleaseVersionNumber$PreReleaseName" |
	sc -Path $SolutionInfoPath -Encoding UTF8
# Set the copyright
$Copyright = "Copyright © Shannon Deminick " + (Get-Date).year;
(gc -Path $SolutionInfoPath) `
	-replace "(?<=AssemblyCopyright\(`").*(?=`"\))", $Copyright |
	sc -Path $SolutionInfoPath -Encoding UTF8;

# Build the solution in release mode
$SolutionPath = Join-Path -Path $SolutionRoot -ChildPath "Articulate.sln";

# clean sln for all deploys
& $MSBuild "$SolutionPath" /p:Configuration=Release /maxcpucount /t:Clean
if (-not $?)
{
	throw "The MSBuild process returned an error code."
}

#build
& $MSBuild "$SolutionPath" /p:Configuration=Release /maxcpucount
if (-not $?)
{
	throw "The MSBuild process returned an error code."
}

####### DO THE UMBRACO PACKAGE BUILD #############

# Set the version number in createdPackages.config
$CreatedPackagesConfig = Join-Path -Path $BuildFolder -ChildPath "createdPackages.config"
$CreatedPackagesConfigXML = [xml](Get-Content $CreatedPackagesConfig)
$CreatedPackagesConfigXML.packages.package.version = "$ReleaseVersionNumber"
$CreatedPackagesConfigXML.Save($CreatedPackagesConfig)

#copy the orig manifest to temp location to be updated to be used for the package
$PackageManifest = Join-Path -Path $BuildFolder -ChildPath "packageManifest.xml"
New-Item -ItemType Directory -Path $TempFolder
Copy-Item $PackageManifest "$TempFolder\package.xml"
$PackageManifest = (Join-Path -Path $TempFolder -ChildPath "package.xml")

# Set the data in packageManifest.config
$PackageManifestXML = [xml](Get-Content $PackageManifest)
$PackageManifestXML.umbPackage.info.package.version = "$ReleaseVersionNumber"
$PackageManifestXML.umbPackage.info.package.name = $CreatedPackagesConfigXML.packages.package.name
$PackageManifestXML.umbPackage.info.package.license.set_InnerXML($CreatedPackagesConfigXML.packages.package.license.get_InnerXML())
$PackageManifestXML.umbPackage.info.package.license.url = $CreatedPackagesConfigXML.packages.package.license.url
$PackageManifestXML.umbPackage.info.package.url = $CreatedPackagesConfigXML.packages.package.url
$PackageManifestXML.umbPackage.info.author.name = $CreatedPackagesConfigXML.packages.package.author.get_InnerXML()
$PackageManifestXML.umbPackage.info.author.website = $CreatedPackagesConfigXML.packages.package.author.url

#clear the files from the manifest
$NewFilesXML = $PackageManifestXML.CreateElement("files")

#package the files ... This will lookup all files in the file system that need to be there and update
# the package manifest XML with the correct data along with copying these files to the  temp folder 
# so they can be zipped with the package

Function WritePackageFile ($f)
{
	Write-Host $f.FullName -foregroundcolor cyan
	$NewFileXML = $PackageManifestXML.CreateElement("file")
	$NewFileXML.set_InnerXML("<guid></guid><orgPath></orgPath><orgName></orgName>")
	$GuidName = ([guid]::NewGuid()).ToString() + "_" + $f.Name
	$NewFileXML.guid = $GuidName	
	$NewFileXML.orgPath = ReverseMapPath $f
	$NewFileXML.orgName = $f.Name
	$NewFilesXML.AppendChild($NewFileXML)
	Copy-Item $f.FullName "$TempFolder\$GuidName"
}
Function ReverseMapPath ($f)
{
	$resultPath = "~"+ $f.Directory.FullName.Replace($WebProjFolder, "").Replace("\","/")	
	Return $resultPath
}
Function MapPath ($f)
{
	$resultPath = Join-Path -Path $WebProjFolder -ChildPath ($f.Replace("~", "").Replace("/", "\"))
	Return $resultPath
}
foreach($FileXML in $CreatedPackagesConfigXML.packages.package.files.file)
{
	$File = Get-Item (MapPath $FileXML)
    if ($File -is [System.IO.DirectoryInfo]) 
    {
        Get-ChildItem -path $File -Recurse `
			| Where-Object { $_ -isnot [System.IO.DirectoryInfo]} `
			| ForEach-Object { WritePackageFile($_) } `
		    | Out-Null	
    }
	else {
		WritePackageFile($File)| Out-Null	
	}
}
$PackageManifestXML.umbPackage.ReplaceChild($NewFilesXML, $PackageManifestXML.SelectSingleNode("/umbPackage/files")) | Out-Null
$PackageManifestXML.Save($PackageManifest)

#finally zip the package
$DestZIP = "$ReleaseFolder\Articulate.zip" 
Add-Type -assembly "system.io.compression.filesystem"
[io.compression.zipfile]::CreateFromDirectory($TempFolder, $DestZIP) 