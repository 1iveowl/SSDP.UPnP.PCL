using System.Collections.Frozen;

namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// A NOTIFY advertisement this device is sending: <c>ssdp:alive</c>,
/// <c>ssdp:byebye</c> or <c>ssdp:update</c>. Immutable.
/// </summary>
/// <remarks>
/// A received notification is <see cref="ReceivedNotify"/>, not this. The fields a
/// composer cannot do without are <see langword="required"/> here, which is what
/// used to be a run-time <see cref="SSDPException"/> from the composer.
/// </remarks>
public sealed record Notify
{
    /// <summary>The transport the notification will be sent over.</summary>
    public TransportType NotifyTransportType { get; init; } = TransportType.Multicast;

    /// <summary>The <c>HOST</c> header; the SSDP multicast group for multicast notifications.</summary>
    public string? HOST { get; init; }

    /// <summary>
    /// The advertised lifetime sent as <c>CACHE-CONTROL: max-age</c>, or
    /// <see langword="null"/> to announce none.
    /// </summary>
    /// <remarks>
    /// The header is Required on <c>ssdp:alive</c> (UDA 2.0 section 1.2.2), so an
    /// unset value emits <c>max-age=0</c> rather than omitting it.
    /// </remarks>
    public TimeSpan? MaxAge { get; init; }

    /// <summary>The URL of the device description document (<c>LOCATION</c>); sent for alive and update.</summary>
    public Uri? Location { get; init; }

    /// <summary>The notification type (<c>NT</c> header): the entity URI being advertised.</summary>
    public required string NT { get; init; }

    /// <summary>The notification sub type: alive, byebye or update.</summary>
    public required NTS NTS { get; init; }

    /// <summary>The advertising device's identity (<c>SERVER</c> header); only sent for alive.</summary>
    public Server? Server { get; init; }

    /// <summary>The unique service name of the advertised entity (<c>USN</c> header).</summary>
    public required USN USN { get; init; }

    /// <summary>The advertising device's boot instance (<c>BOOTID.UPNP.ORG</c>).</summary>
    /// <remarks>
    /// Required on alive and byebye, so an unset value is sent as <c>0</c> rather
    /// than omitted.
    /// </remarks>
    public uint? BOOTID { get; init; }

    /// <summary>The advertising device's configuration number (<c>CONFIGID.UPNP.ORG</c>), if any.</summary>
    public int? CONFIGID { get; init; }

    /// <summary>The port for unicast search (<c>SEARCHPORT.UPNP.ORG</c>), if not 1900.</summary>
    public DynamicPort? SEARCHPORT { get; init; }

    /// <summary>The boot instance that takes effect after an <c>ssdp:update</c> (<c>NEXTBOOTID.UPNP.ORG</c>).</summary>
    public uint? NEXTBOOTID { get; init; }

    /// <summary>The HTTPS description URL (<c>SECURELOCATION.UPNP.ORG</c>), if any.</summary>
    public string? SECURELOCATION { get; init; }

    /// <summary>Additional vendor-specific headers.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        FrozenDictionary<string, string>.Empty;
}
