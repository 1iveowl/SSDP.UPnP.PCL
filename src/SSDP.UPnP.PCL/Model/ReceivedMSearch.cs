using System.Collections.Frozen;
using System.Net;

namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// An M-SEARCH request as it arrived, produced by
/// <see cref="Parsing.SsdpMessageParser.ParseMSearchRequest"/>. Immutable.
/// </summary>
/// <remarks>
/// Separate from <see cref="MSearchRequest"/> on purpose. What this library sends
/// it can hold to the specification; what a control point on the network sends it
/// can only report. So the fields here are nullable where the sender may legally
/// or illegally have omitted them, and the diagnostics a sent message has no use
/// for - the raw bytes, the endpoints, the parser's verdict - live here only.
/// </remarks>
public sealed record ReceivedMSearch
{
    /// <summary>Whether the search arrived as multicast or unicast, classified by the <c>HOST</c> header and transport.</summary>
    public TransportType TransportType { get; init; }

    /// <summary>The <c>HOST</c> header as sent.</summary>
    public string? HOST { get; init; }

    /// <summary>
    /// The <c>MX</c> value. <see cref="TimeSpan.Zero"/> for a unicast search, which
    /// carries no MX and is answered at once.
    /// </summary>
    /// <remarks>
    /// A <see cref="TimeSpan"/> rather than an <see cref="MxSeconds"/>: the parser
    /// already rejects a multicast search whose MX is below 1, so a value here is
    /// either legal or the zero that means "unicast, no MX".
    /// </remarks>
    public TimeSpan MX { get; init; }

    /// <summary>The search target. Always present: a search whose ST cannot be parsed is rejected.</summary>
    public required ST ST { get; init; }

    /// <summary>The sender's <c>USER-AGENT</c>.</summary>
    public UserAgent UserAgent { get; init; } = new();

    /// <summary>
    /// Friendly name of the searching control point (<c>CPFN.UPNP.ORG</c>).
    /// </summary>
    /// <remarks>
    /// UDA 2.0 requires it on multicast search, but plenty of real control points
    /// omit it and the specification prohibits answering with an error, so this is
    /// reported rather than enforced.
    /// </remarks>
    public string? CPFN { get; init; }

    /// <summary>UUID of the searching control point (<c>CPUUID.UPNP.ORG</c>), if sent.</summary>
    public string? CPUUID { get; init; }

    /// <summary>
    /// The TCP port to answer on (<c>TCPPORT.UPNP.ORG</c>), if sent. Always in the
    /// dynamic range - the parser rejects the whole search otherwise, since a
    /// device cannot answer on a port the specification forbids.
    /// </summary>
    public DynamicPort? TCPPORT { get; init; }

    /// <summary>Non-standard headers the sender included.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        FrozenDictionary<string, string>.Empty;

    /// <summary>
    /// The message exactly as it arrived, before parsing or header normalization,
    /// when raw capture is enabled on the receiving device. Empty otherwise, and
    /// always empty for messages received over TCP.
    /// </summary>
    public ReadOnlyMemory<byte> RawMessage { get; init; }

    /// <summary>The local endpoint the request arrived on.</summary>
    public IPEndPoint? LocalIpEndPoint { get; init; }

    /// <summary>The endpoint of the requester.</summary>
    public IPEndPoint? RemoteIpEndPoint { get; init; }

    /// <summary>Whether the underlying HTTP parser flagged errors in the received message.</summary>
    public bool HasParsingError { get; init; }
}
