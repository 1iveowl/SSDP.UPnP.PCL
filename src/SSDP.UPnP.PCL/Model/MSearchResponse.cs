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

    /// <summary>
    /// The advertised lifetime from <c>CACHE-CONTROL: max-age</c>, or
    /// <see langword="null"/> when the sender announced none.
    /// </summary>
    /// <remarks>
    /// <see cref="TimeSpan.Zero"/> and <see langword="null"/> mean different things
    /// and call for opposite handling: zero is a device asking to be expired now,
    /// null is a device that said nothing, leaving the lifetime to the consumer's
    /// own default. Null also covers a header with no <c>max-age</c> directive and
    /// an invalid value. A <c>ssdp:byebye</c> carries no <c>CACHE-CONTROL</c> at
    /// all, so null there is normal.
    /// </remarks>
    public TimeSpan? MaxAge { get; init; }

    /// <summary>The <c>DATE</c> header value.</summary>
    /// <remarks><see langword="null"/> when the response carried no <c>DATE</c> header; it is Recommended rather than Required.</remarks>
    public DateTimeOffset? Date { get; init; }

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
    /// <remarks>
    /// <see langword="null"/> when the response carried no <c>BOOTID.UPNP.ORG</c>,
    /// which UPnP 1.0 devices do not send. A device that sent <c>0</c> is a
    /// different thing from one that sent nothing; see <see cref="NLS"/>.
    /// </remarks>
    public uint? BOOTID { get; init; }

    /// <summary>The responding device's configuration number (<c>CONFIGID.UPNP.ORG</c>), if any.</summary>
    public int? CONFIGID { get; init; }

    /// <summary>The port for unicast search (<c>SEARCHPORT.UPNP.ORG</c>), if not 1900.</summary>
    public int? SEARCHPORT { get; init; }

    /// <summary>The HTTPS description URL (<c>SECURELOCATION.UPNP.ORG</c>), if any.</summary>
    public string? SECURELOCATION { get; init; }

    /// <summary>
    /// The UPnP 1.0 Network Location Signature (<c>NLS</c>), when the responder
    /// carried one. It serves the purpose <see cref="BOOTID"/> later took over:
    /// the value changes when the device reboots. Opaque - implementations have
    /// used both integers and GUID-shaped strings - and advisory only, since it
    /// is not a UDA-normative header.
    /// </summary>
    public string? NLS { get; init; }

    /// <summary>The MX value of the search being answered; bounds the response delay.</summary>
    public TimeSpan MX { get; init; }

    /// <summary>Additional vendor-specific headers.</summary>
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

    /// <summary>For received responses: the local endpoint the response arrived on.</summary>
    public IPEndPoint? LocalIpEndPoint { get; init; }

    /// <summary>For received responses: the responder's endpoint. For composed responses: the requester to reply to.</summary>
    public IPEndPoint? RemoteIpEndPoint { get; init; }

    /// <summary>Whether the underlying HTTP parser flagged errors in the received message.</summary>
    public bool HasParsingError { get; init; }
}
