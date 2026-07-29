using System.Net;

namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// The configuration of a root device: the device itself plus the description
/// document location, server identity and any embedded devices. Immutable.
/// </summary>
public sealed record RootDeviceConfiguration : DeviceConfiguration
{
    /// <summary>
    /// The local endpoint the device answers unicast messages on. When constructing a
    /// <see cref="Device"/> from a configuration this selects the network interface to
    /// bind; when constructing from prepared UDP clients it is derived from them.
    /// </summary>
    public IPEndPoint? IpEndPoint { get; init; }

    /// <summary>The identity sent in <c>SERVER</c> headers.</summary>
    public Server Server { get; init; } = new();

    /// <summary>The URL of the device description document (<c>LOCATION</c> header).</summary>
    /// <remarks>
    /// Required on every advertisement and every search response (UDA 2.0 sections
    /// 1.2.2 and 1.3.3), so a device without one cannot compose a conforming
    /// message. It is the source of every LOCATION this device sends.
    /// </remarks>
    public required Uri Location { get; init; }

    /// <summary>The HTTPS URL of the device description document (<c>SECURELOCATION.UPNP.ORG</c>), if any.</summary>
    public Uri? SecureLocation { get; init; }

    /// <summary>
    /// The configuration number sent as <c>CONFIGID.UPNP.ORG</c>. Required by
    /// UDA 2.0 in all announcements; freely assignable values are 0–16777215
    /// (2^24−1) and the value must change whenever the description changes.
    /// Defaults to 0.
    /// </summary>
    public int CONFIGID { get; init; }

    /// <summary>Advertisement validity (<c>CACHE-CONTROL: max-age</c>).</summary>
    public TimeSpan CacheControl { get; init; } = TimeSpan.FromSeconds(1800);

    /// <summary>Devices embedded in this root device.</summary>
    public IReadOnlyList<DeviceConfiguration> EmbeddedDevices { get; init; } = [];
}
