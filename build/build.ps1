$PSScriptFilePath = Get-Item $MyInvocation.MyCommand.Path
$RepoRoot = $PSScriptFilePath.Directory.Parent.FullName
$BuildFolder = Join-Path -Path $RepoRoot -ChildPath "build";
$WebProjFolder = Join-Path -Path $RepoRoot -ChildPath "src\Articulate.Web";
$ReleaseFolder = Join-Path -Path $BuildFolder -ChildPath "Release";
$SolutionRoot = Join-Path -Path $RepoRoot "src";
$CodeProjFolder = Join-Path -Path $RepoRoot -ChildPath "src\Articulate";
$CodeCSProj = Join-Path -Path $CodeProjFolder -ChildPath "Articulate.csproj";

# Go get nuget.exe if we don't hae it
$NuGet = "$BuildFolder\nuget.exe"
$FileExists = Test-Path $NuGet 
If ($FileExists -eq $False) {
	Write-Host "Retrieving nuget.exe..."
	$SourceNugetExe = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
	Invoke-WebRequest $SourceNugetExe -OutFile $NuGet
}

# ensure we have vswhere
New-Item "$BuildFolder\vswhere" -type directory -force
$vswhere = "$BuildFolder\vswhere.exe"
if (-not (test-path $vswhere))
{
	Write-Host "Download VsWhere..."
    $path = "$ToolsFolder\tmp"
    &$nuget install vswhere -OutputDirectory $path
    $dir = ls "$path\vswhere.*" | sort -property Name -descending | select -first 1
    $file = ls -path "$dir" -name vswhere.exe -recurse
    mv "$dir\$file" $vswhere   
}

$MSBuild = &$vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | select-object -first 1
if (-not (test-path $MSBuild)) {
    throw "MSBuild not found!"
}


if ((Get-Item $ReleaseFolder -ErrorAction SilentlyContinue) -ne $null)
{
	Write-Warning "$ReleaseFolder already exists on your local machine. It will now be deleted."
	Remove-Item $ReleaseFolder -Recurse
}

####### DO THE SLN BUILD PART #############

# Build the solution in release mode
$SolutionPath = Join-Path -Path $SolutionRoot -ChildPath "Articulate.sln";

#restore nuget packages
Write-Host "Restoring nuget packages..."
& $NuGet restore $SolutionPath

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

# dotnet pack (As its a SDK style project, nuget pack was not reading info stored in csproj)
& dotnet pack $CodeCSProj --output $ReleaseFolder --configuration Release
