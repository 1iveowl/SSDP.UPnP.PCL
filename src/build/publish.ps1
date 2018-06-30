param([string]$betaver)

if ([string]::IsNullOrEmpty($betaver)) {
	$version = [Reflection.AssemblyName]::GetAssemblyName((resolve-path '..\interfaces\ISSDP.UPnP.Netstandard\bin\Release\netstandard2.0\ISSDP.UPnP.PCL.dll')).Version.ToString(3)
	}
else {
	$version = [Reflection.AssemblyName]::GetAssemblyName((resolve-path '..\interfaces\ISSDP.UPnP.Netstandard\bin\Release\netstandard2.0\ISSDP.UPnP.PCL.dll')).Version.ToString(3) + "-" + $betaver
}

.\build.ps1 $version

Nuget.exe push ".\NuGet\SSDP.UPnP.PCL.$version.symbols.nupkg" -Source https://www.nuget.org