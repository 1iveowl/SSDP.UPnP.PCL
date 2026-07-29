using System.Collections.Frozen;
using System.Net;

namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// A NOTIFY advertisement as it arrived, observed via
/// <see cref="IControlPoint.NotifyObservable"/>. Immutable.
/// </summary>
/// <remarks>
/// Separate from <see cref="Notify"/> on purpose: a composed notification can be
/// held to the specification, an observed one can only be reported. Every field a
/// real device might omit is nullable here, including ones UDA 2.0 marks Required.
/// </remarks>
public sealed record ReceivedNotify
{
    /// <summary>The transport the notification arrived over.</summary>
    public TransportType NotifyTransportType { get; init; } = TransportType.Multicast;

    /// <summary>The <c>HOST</c> header as sent.</summary>
    public string? HOST { get; init; }

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

    /// <summary>The URL of the device description document (<c>LOCATION</c>).</summary>
    public Uri? Location { get; init; }

    /// <summary>The notification type (<c>NT</c> header).</summary>
    public string? NT { get; init; }

    /// <summary>The notification sub type; <see cref="Model.NTS.Unknown"/> when absent or unrecognized.</summary>
    public NTS NTS { get; init; }

    /// <summary>The advertising device's identity (<c>SERVER</c> header).</summary>
    public Server? Server { get; init; }

    /// <summary>The unique service name of the advertised entity (<c>USN</c> header).</summary>
    public USN? USN { get; init; }

    /// <summary>The advertising device's boot instance (<c>BOOTID.UPNP.ORG</c>).</summary>
    /// <remarks>
    /// <see langword="null"/> when the message carried none - UPnP 1.0 devices
    /// predate the header. A device that sent <c>0</c> is a different thing from
    /// one that sent nothing, so this is not defaulted; see <see cref="NLS"/> for
    /// the 1.0 equivalent.
    /// </remarks>
    public uint? BOOTID { get; init; }

    /// <summary>The advertising device's configuration number (<c>CONFIGID.UPNP.ORG</c>), if any.</summary>
    public int? CONFIGID { get; init; }

    /// <summary>
    /// The port for unicast search (<c>SEARCHPORT.UPNP.ORG</c>) as sent.
    /// </summary>
    /// <remarks>
    /// An <see cref="int"/> rather than a <see cref="DynamicPort"/>: UDA 2.0
    /// restricts the value to 49152-65535, but a device that ignores that still
    /// gets reported here rather than having its advertisement dropped.
    /// </remarks>
    public int? SEARCHPORT { get; init; }

    /// <summary>The boot instance that takes effect after an <c>ssdp:update</c> (<c>NEXTBOOTID.UPNP.ORG</c>).</summary>
    public uint? NEXTBOOTID { get; init; }

    /// <summary>The HTTPS description URL (<c>SECURELOCATION.UPNP.ORG</c>), if any.</summary>
    public string? SECURELOCATION { get; init; }

    /// <summary>
    /// The UPnP 1.0 Network Location Signature (<c>NLS</c>), when the sender
    /// carried one. It serves the purpose <see cref="BOOTID"/> later took over:
    /// the value changes when the device reboots. Opaque - implementations have
    /// used both integers and GUID-shaped strings - and advisory only, since it
    /// is not a UDA-normative header.
    /// </summary>
    public string? NLS { get; init; }

    /// <summary>Whether the USN device UUID is a well-formed GUID as UDA 2.0 requires.</summary>
    public bool IsUuidUpnp2Compliant { get; init; }

    /// <summary>Additional vendor-specific headers.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        FrozenDictionary<string, string>.Empty;

    /// <summary>
    /// The message exactly as it arrived, before parsing or header normalization,
    /// when raw capture is enabled on the receiving control point. Empty otherwise,
    /// and always empty for messages received over TCP.
    /// </summary>
    /// <remarks>
    /// Useful when a device's own formatting matters: <see cref="Headers"/> is
    /// normalized to uppercase names with repeated fields comma-joined, while this
    /// preserves what the sender actually wrote.
    /// </remarks>
    public ReadOnlyMemory<byte> RawMessage { get; init; }

    /// <summary>The local endpoint the message arrived on.</summary>
    public IPEndPoint? LocalIpEndPoint { get; init; }

    /// <summary>The sender's endpoint.</summary>
    public IPEndPoint? RemoteIpEndPoint { get; init; }

    /// <summary>Whether the underlying HTTP parser flagged errors in the received message.</summary>
    public bool HasParsingError { get; init; }
}
