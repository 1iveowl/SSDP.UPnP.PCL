$msbuild = join-path -path (Get-ItemProperty "HKLM:\software\Microsoft\MSBuild\ToolsVersions\14.0")."MSBuildToolsPath" -childpath "msbuild.exe"
&$msbuild ..\interfaces\ISSDP.UPnP.PCL.csproj /t:Build /p:Configuration="Release"
&$msbuild ..\main\SDPP.UPnP.PCL.csproj /t:Build /p:Configuration="Release"


$version = [Reflection.AssemblyName]::GetAssemblyName((resolve-path '..\interfaces\bin\release\ISSDP.UPnP.PCL.dll')).Version.ToString(3)
Remove-Item .\NuGet -Force -Recurse
New-Item -ItemType Directory -Force -Path .\NuGet
NuGet.exe pack SDPP.UPnP.PCL.nuspec -Verbosity detailed -Symbols -OutputDir "NuGet" -Version $version

Nuget.exe push ".\NuGet\SSDP.UPnP.PCL.$version.symbols.nupkg" -Source https://www.nuget.org