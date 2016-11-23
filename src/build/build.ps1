#################################################
## IMPORTANT: Using MS Build Tool for VS 2017! ##
## This project include C# 7.0 features        ##
#################################################

## Install VS 2017 MSBuild tools from here: https://www.visualstudio.com/downloads/#build-tools-for-visual-studio-2017-rc

param([string]$betaver)

$msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2017\BuildTools\MSBuild\15.0\Bin\msbuild.exe"

&$msbuild ..\interfaces\ISSDP.UPnP.PCL.csproj /t:Build /p:Configuration="Release"
&$msbuild ..\main\SDPP.UPnP.PCL.csproj /t:Build /p:Configuration="Release"

if ([string]::IsNullOrEmpty($betaver)) {
	$version = [Reflection.AssemblyName]::GetAssemblyName((resolve-path '..\interfaces\bin\release\ISSDP.UPnP.PCL.dll')).Version.ToString(3)
	}
else {
	$version = [Reflection.AssemblyName]::GetAssemblyName((resolve-path '..\interfaces\bin\release\ISSDP.UPnP.PCL.dll')).Version.ToString(3) + "-" + $betaver
}

Remove-Item .\NuGet -Force -Recurse
New-Item -ItemType Directory -Force -Path .\NuGet
NuGet.exe pack SDPP.UPnP.PCL.nuspec -Verbosity detailed -Symbols -OutputDir "NuGet" -Version $version