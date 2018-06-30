param([string]$betaver)

if ([string]::IsNullOrEmpty($betaver)) {
	$version = [Reflection.AssemblyName]::GetAssemblyName((resolve-path '..\interfaces\ISSDP.UPnP.Netstandard\bin\Release\netstandard2.0\ISSDP.UPnP.PCL.dll')).Version.ToString(3)
	}
else {
	$version = [Reflection.AssemblyName]::GetAssemblyName((resolve-path '..\interfaces\ISSDP.UPnP.Netstandard\bin\Release\netstandard2.0\ISSDP.UPnP.PCL.dll')).Version.ToString(3) + "-" + $betaver
}

.\build.ps1 $version

nuget.exe push -Source "1iveowlNuGetRepo" -ApiKey key ".\NuGet\SSDP.UPnP.PCL.$version.symbols.nupkg"
