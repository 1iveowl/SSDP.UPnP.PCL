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

    /// <summary>The <c>TCPPORT.UPNP.ORG</c> header, if any.</summary>
    public string? TCPPORT { get; init; }

    /// <summary>Additional vendor-specific headers to send, or the non-standard headers received.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>For received requests: the local endpoint the request arrived on.</summary>
    public IPEndPoint? LocalIpEndPoint { get; init; }

    /// <summary>For received requests: the endpoint of the requester. For unicast sends: the target endpoint.</summary>
    public IPEndPoint? RemoteIpEndPoint { get; init; }

    /// <summary>Whether the underlying HTTP parser flagged errors in the received message.</summary>
    public bool HasParsingError { get; init; }
}
