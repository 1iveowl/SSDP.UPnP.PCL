using System.Collections.Frozen;

namespace SSDP.UPnP.PCL.Internal;

/// <summary>
/// The SSDP header names, declared once, together with the set of them that counts
/// as standard for each message kind.
/// </summary>
/// <remarks>
/// These were previously spelled out as literals in both
/// <see cref="Parsing.DatagramComposer"/> and <see cref="Parsing.SsdpMessageParser"/>,
/// which made every new header a two-place edit with a silent failure mode: compose
/// a header but forget to add it to the parser's standard set, and on receive it
/// reappears in <c>Headers</c> as though the sender had invented it.
/// </remarks>
internal static class SsdpHeaders
{
    internal const string BootId = "BOOTID.UPNP.ORG";
    internal const string CacheControl = "CACHE-CONTROL";
    internal const string ConfigId = "CONFIGID.UPNP.ORG";
    internal const string Cpfn = "CPFN.UPNP.ORG";
    internal const string Cpuuid = "CPUUID.UPNP.ORG";
    internal const string Date = "DATE";
    internal const string Ext = "EXT";
    internal const string Host = "HOST";
    internal const string Location = "LOCATION";
    internal const string Man = "MAN";
    internal const string Mx = "MX";
    internal const string NextBootId = "NEXTBOOTID.UPNP.ORG";
    internal const string Nt = "NT";
    internal const string Nts = "NTS";
    internal const string Opt = "OPT";
    internal const string SearchPort = "SEARCHPORT.UPNP.ORG";
    internal const string SecureLocation = "SECURELOCATION.UPNP.ORG";
    internal const string Server = "SERVER";
    internal const string St = "ST";
    internal const string TcpPort = "TCPPORT.UPNP.ORG";
    internal const string UserAgent = "USER-AGENT";
    internal const string Usn = "USN";

    /// <summary>
    /// Headers a received M-SEARCH request carries by specification; anything else
    /// is surfaced to the consumer as a vendor header.
    /// </summary>
    internal static readonly FrozenSet<string> MSearchRequestStandard = ToSet(
        Host, CacheControl, Man, Mx, St, UserAgent, Cpfn, Cpuuid, TcpPort, SecureLocation);

    /// <summary>Headers a received M-SEARCH response carries by specification.</summary>
    internal static readonly FrozenSet<string> MSearchResponseStandard = ToSet(
        Host, CacheControl, Location, Date, Ext, Server, St, Usn,
        BootId, ConfigId, SearchPort, SecureLocation);

    /// <summary>Headers a received NOTIFY carries by specification.</summary>
    internal static readonly FrozenSet<string> NotifyStandard = ToSet(
        Host, CacheControl, Location, Nt, Nts, Server, Usn,
        BootId, ConfigId, SearchPort, NextBootId, SecureLocation);

    // Header names are case-insensitive (UDA 2.0 section 1.3.2), and the listener
    // normalizes them to uppercase, so the comparer matters only for messages that
    // arrive by another route.
    private static FrozenSet<string> ToSet(params string[] names) =>
        names.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
