param([string]$betaver)

.\build.ps1 $betaver

if ([string]::IsNullOrEmpty($betaver)) {
	$version = [Reflection.AssemblyName]::GetAssemblyName((resolve-path '..\interfaces\bin\release\ISSDP.UPnP.PCL.dll')).Version.ToString(3)
	}
else {
	$version = [Reflection.AssemblyName]::GetAssemblyName((resolve-path '..\interfaces\bin\release\ISSDP.UPnP.PCL.dll')).Version.ToString(3) + "-" + $betaver
}

nuget.exe push -Source "1iveowlNuGetRepo" -ApiKey key ".\NuGet\SSDP.UPnP.PCL.$version.symbols.nupkg"
