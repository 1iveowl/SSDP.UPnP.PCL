namespace SSDP.UPnP.PCL.Analyzers;

/// <summary>
/// Diagnostic identifiers and the property keys code fixes read, shared with the
/// code-fix assembly as linked source.
/// </summary>
/// <remarks>
/// SSDP002, SSDP004 and SSDP006 are absent on purpose. Two were planned and then
/// deleted by the 10.0 type split rather than written - a multicast search cannot
/// omit CPFN, a unicast search cannot omit its target, and there is no third
/// transport - and SSDP006 is deferred. The identifiers stay reserved so a future
/// rule cannot reuse one and quietly change what somebody's existing suppression
/// means.
/// </remarks>
internal static class DiagnosticIds
{
    /// <summary>Multicast M-SEARCH MX above the 5 seconds UDA 2.0 recommends.</summary>
    internal const string MxAboveRecommendedMaximum = "SSDP001";

    /// <summary>TCPPORT.UPNP.ORG outside the 49152-65535 range UDA 2.0 mandates.</summary>
    internal const string TcpPortOutOfRange = "SSDP003";

    /// <summary>A device configuration value outside the range UDA 2.0 mandates.</summary>
    internal const string DeviceConfigurationOutOfRange = "SSDP005";

    /// <summary>
    /// The category every rule in this package reports under.
    /// </summary>
    internal const string Category = "Usage";

    /// <summary>
    /// Property key carrying the replacement value a code fix should substitute.
    /// </summary>
    internal const string ReplacementValueKey = "ReplacementValue";

    internal static string HelpLink(string diagnosticId) =>
        "https://github.com/1iveowl/SSDP.UPnP.PCL#" + diagnosticId.ToLowerInvariant();
}
