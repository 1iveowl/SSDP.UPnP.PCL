using System.Collections.Frozen;
using System.Net;

namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// An M-SEARCH response this device is sending. Immutable.
/// </summary>
/// <remarks>
/// A received response is <see cref="ReceivedMSearchResponse"/>, not this.
/// <see cref="ST"/> and <see cref="USN"/> are <see langword="required"/>, which is
/// what used to be a run-time <see cref="SSDPException"/> from the composer: a
/// response identifying nothing is not a response.
/// </remarks>
public sealed record MSearchResponse
{
    /// <summary>HTTP status code; <c>200</c> for well-formed responses.</summary>
    public int StatusCode { get; init; } = 200;

    /// <summary>HTTP reason phrase; <c>OK</c> for well-formed responses.</summary>
    public string ResponseReason { get; init; } = "OK";

    /// <summary>
    /// The advertised lifetime sent as <c>CACHE-CONTROL: max-age</c>. The header is
    /// Required (UDA 2.0 section 1.3.3), so an unset value emits <c>max-age=0</c>
    /// rather than omitting it.
    /// </summary>
    public TimeSpan? MaxAge { get; init; }

    /// <summary>
    /// The <c>DATE</c> header value, or <see langword="null"/> to omit it - it is
    /// Recommended rather than Required.
    /// </summary>
    public DateTimeOffset? Date { get; init; }

    /// <summary>The URL of the device description document (<c>LOCATION</c>).</summary>
    public Uri? Location { get; init; }

    /// <summary>The responding device's identity (<c>SERVER</c> header).</summary>
    public Server Server { get; init; } = new();

    /// <summary>The search target the response answers (<c>ST</c> header).</summary>
    public required ST ST { get; init; }

    /// <summary>The unique service name of the responding entity (<c>USN</c> header).</summary>
    public required USN USN { get; init; }

    /// <summary>The responding device's boot instance (<c>BOOTID.UPNP.ORG</c>); sent as <c>0</c> when unset.</summary>
    public uint? BOOTID { get; init; }

    /// <summary>The responding device's configuration number (<c>CONFIGID.UPNP.ORG</c>), if any.</summary>
    public int? CONFIGID { get; init; }

    /// <summary>The port for unicast search (<c>SEARCHPORT.UPNP.ORG</c>), if not 1900.</summary>
    public DynamicPort? SEARCHPORT { get; init; }

    /// <summary>The HTTPS description URL (<c>SECURELOCATION.UPNP.ORG</c>), if any.</summary>
    public string? SECURELOCATION { get; init; }

    /// <summary>The MX value of the search being answered; bounds the response delay.</summary>
    public TimeSpan MX { get; init; }

    /// <summary>Additional vendor-specific headers.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        FrozenDictionary<string, string>.Empty;

    /// <summary>The requester to reply to.</summary>
    public IPEndPoint? RemoteIpEndPoint { get; init; }
}
