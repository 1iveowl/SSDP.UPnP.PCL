using System.Collections.Frozen;
using System.Net;

namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// An SSDP M-SEARCH response, either observed by a control point via
/// <see cref="IControlPoint.MSearchResponseObservable"/> or composed by a device
/// answering a search. Immutable.
/// </summary>
public sealed record MSearchResponse
{
    /// <summary>The transport the response was (or will be) sent over.</summary>
    public TransportType TransportType { get; init; } = TransportType.Unicast;

    /// <summary>HTTP status code; <c>200</c> for well-formed responses.</summary>
    public int StatusCode { get; init; } = 200;

    /// <summary>HTTP reason phrase; <c>OK</c> for well-formed responses.</summary>
    public string ResponseReason { get; init; } = "OK";

    /// <summary>Advertisement validity (<c>CACHE-CONTROL: max-age</c>).</summary>
    public TimeSpan CacheControl { get; init; }

    /// <summary>The <c>DATE</c> header value.</summary>
    public DateTimeOffset Date { get; init; }

    /// <summary>The URL of the device description document (<c>LOCATION</c>).</summary>
    public Uri? Location { get; init; }

    /// <summary>Whether the <c>EXT</c> header was present (required by UDA for responses).</summary>
    public bool Ext { get; init; } = true;

    /// <summary>The responding device's identity (<c>SERVER</c> header).</summary>
    public Server Server { get; init; } = new();

    /// <summary>The search target the response answers (<c>ST</c> header).</summary>
    public ST? ST { get; init; }

    /// <summary>The unique service name of the responding entity (<c>USN</c> header).</summary>
    public USN? USN { get; init; }

    /// <summary>The responding device's boot instance (<c>BOOTID.UPNP.ORG</c>).</summary>
    public uint BOOTID { get; init; }

    /// <summary>The responding device's configuration number (<c>CONFIGID.UPNP.ORG</c>), if any.</summary>
    public int? CONFIGID { get; init; }

    /// <summary>The port for unicast search (<c>SEARCHPORT.UPNP.ORG</c>), if not 1900.</summary>
    public int? SEARCHPORT { get; init; }

    /// <summary>The HTTPS description URL (<c>SECURELOCATION.UPNP.ORG</c>), if any.</summary>
    public string? SECURELOCATION { get; init; }

    /// <summary>The MX value of the search being answered; bounds the response delay.</summary>
    public TimeSpan MX { get; init; }

    /// <summary>Additional vendor-specific headers.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        FrozenDictionary<string, string>.Empty;

    /// <summary>For received responses: the local endpoint the response arrived on.</summary>
    public IPEndPoint? LocalIpEndPoint { get; init; }

    /// <summary>For received responses: the responder's endpoint. For composed responses: the requester to reply to.</summary>
    public IPEndPoint? RemoteIpEndPoint { get; init; }

    /// <summary>Whether the underlying HTTP parser flagged errors in the received message.</summary>
    public bool HasParsingError { get; init; }
}
