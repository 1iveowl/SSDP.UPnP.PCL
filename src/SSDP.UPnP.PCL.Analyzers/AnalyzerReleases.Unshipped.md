; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SSDP001 | Usage | Warning | Multicast M-SEARCH MX above the 5 seconds UDA 2.0 recommends. [Documentation](https://github.com/1iveowl/SSDP.UPnP.PCL#ssdp001)
SSDP003 | Usage | Warning | SSDP port outside the 49152-65535 range UDA 2.0 mandates. [Documentation](https://github.com/1iveowl/SSDP.UPnP.PCL#ssdp003)
SSDP005 | Usage | Warning | Device configuration value outside the range UDA 2.0 mandates. [Documentation](https://github.com/1iveowl/SSDP.UPnP.PCL#ssdp005)
