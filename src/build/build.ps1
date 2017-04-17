#################################################
## IMPORTANT: Using MS Build Tool for VS 2017! ##
## This project include C# 7.0 features        ##
#################################################

## Install VS 2017 MSBuild tools from here: https://www.visualstudio.com/downloads/#build-tools-for-visual-studio-2017-rc

param([string]$version)

if ([string]::IsNullOrEmpty($version)) {$version = "0.0.1"}

$msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe"
&$msbuild ..\interfaces\ISSDP.UPnP.Netstandard\ISSDP.UPnP.Netstandard.csproj /t:Build /p:Configuration="Release"
&$msbuild ..\main\SSDP.UPnP.Netstandard\SSDP.UPnP.Netstandard.csproj /t:Build /p:Configuration="Release"


Remove-Item .\NuGet -Force -Recurse
New-Item -ItemType Directory -Force -Path .\NuGet
NuGet.exe pack SDPP.UPnP.PCL.nuspec -Verbosity detailed -Symbols -OutputDir "NuGet" -Version $version