.\build.ps1

$version = [Reflection.AssemblyName]::GetAssemblyName((resolve-path '..\interfaces\bin\release\ISSDP.UPnP.PCL.dll')).Version.ToString(3)

nuget.exe push -Source "1iveowlNuGetRepo" -ApiKey key ".\NuGet\SSDP.UPnP.PCL.$version.symbols.nupkg"
