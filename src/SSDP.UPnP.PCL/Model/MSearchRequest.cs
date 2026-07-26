using System.Collections.Frozen;
using System.Net;

namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// An SSDP M-SEARCH request, either composed for sending via
/// <see cref="IControlPoint.SendMSearchAsync"/> or parsed from a received
/// datagram by <see cref="Parsing.SsdpMessageParser.ParseMSearchRequest"/>. Immutable.
/// </summary>
public sealed record MSearchRequest
{
    /// <summary>Whether the search is multicast or unicast.</summary>
    public TransportType TransportType { get; init; } = TransportType.Multicast;

    /// <summary>The <c>HOST</c> header; only used for unicast searches (multicast always targets the SSDP group).</summary>
    public string? HOST { get; init; }

    /// <summary>Maximum response delay in seconds (<c>MX</c> header); UDA 2.0 limits it to 1–5 seconds.</summary>
    public TimeSpan MX { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>The search target.</summary>
    public required ST ST { get; init; }

    /// <summary>The control point identity sent in the <c>USER-AGENT</c> header.</summary>
    public UserAgent UserAgent { get; init; } = new();

    /// <summary>Friendly name of the control point (<c>CPFN.UPNP.ORG</c>, required by UDA 2.0 for multicast).</summary>
    public string? CPFN { get; init; }

    /// <summary>UUID of the control point (<c>CPUUID.UPNP.ORG</c>), if any.</summary>
    public string? CPUUID { get; init; }

    /// <summary>
    /// The TCP port for reliable search responses (<c>TCPPORT.UPNP.ORG</c>), if any.
    /// Per UDA 2.0 the value must be in the range 49152–65535; when set on a
    /// multicast search, devices reply over TCP to this port instead of UDP.
    /// </summary>
    public int? TCPPORT { get; init; }

    /// <summary>
    /// How many times <see cref="IControlPoint.SendMSearchAsync"/> transmits a
    /// multicast search. UDA 2.0 recommends sending each M-SEARCH more than once
    /// (UDP is unreliable); defaults to 2. Unicast searches are sent once.
    /// </summary>
    public int SendCount { get; init; } = 2;

    /// <summary>
    /// Additional vendor-specific headers to send, or the non-standard headers
    /// received. Note that each SSDP message must fit in a single UDP packet
    /// (UDA 2.0 §1.2.2) — keep vendor headers small.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        FrozenDictionary<string, string>.Empty;

    /// <summary>
    /// The message exactly as it arrived, before parsing or header normalization,
    /// when raw capture is enabled on the receiving control point or device. Empty
    /// otherwise, and always empty for messages received over TCP.
    /// </summary>
    /// <remarks>
    /// Useful when a device's own formatting matters: <see cref="Headers"/> is
    /// normalized to uppercase names with repeated fields comma-joined, while this
    /// preserves what the sender actually wrote.
    /// </remarks>
    public ReadOnlyMemory<byte> RawMessage { get; init; }

    /// <summary>For received requests: the local endpoint the request arrived on.</summary>
    public IPEndPoint? LocalIpEndPoint { get; init; }

    /// <summary>For received requests: the endpoint of the requester. For unicast sends: the target endpoint.</summary>
    public IPEndPoint? RemoteIpEndPoint { get; init; }

    /// <summary>Whether the underlying HTTP parser flagged errors in the received message.</summary>
    public bool HasParsingError { get; init; }
}
