namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// The product information carried by the SSDP <c>SERVER</c> and <c>USER-AGENT</c>
/// headers: <c>OS/version UPnP/major.minor product/version</c>. Immutable.
/// </summary>
public record DeviceInfo
{
    /// <summary>The unparsed header value, when this instance was produced by parsing.</summary>
    public string? FullString { get; init; }

    /// <summary>Operating system name.</summary>
    public string? OperatingSystem { get; init; }

    /// <summary>Operating system version.</summary>
    public string? OperatingSystemVersion { get; init; }

    /// <summary>Product name.</summary>
    public string? ProductName { get; init; }

    /// <summary>Product version.</summary>
    public string? ProductVersion { get; init; }

    /// <summary>UPnP architecture major version; defaults to <c>"2"</c>.</summary>
    public string UpnpMajorVersion { get; init; } = "2";

    /// <summary>UPnP architecture minor version; defaults to <c>"0"</c>.</summary>
    public string UpnpMinorVersion { get; init; } = "0";

    /// <summary>Whether the sender declared UPnP 2.x support.</summary>
    public bool IsUpnp2 { get; init; }

    /// <summary>
    /// The SSDP wire representation:
    /// <c>OS/version UPnP/major.minor product/version</c>.
    /// </summary>
    public string ToHeaderString() =>
        $"{OperatingSystem}/{OperatingSystemVersion} UPnP/{UpnpMajorVersion}.{UpnpMinorVersion} {ProductName}/{ProductVersion}";
}

/// <summary>
/// The device description sent in the <c>SERVER</c> header of responses and
/// NOTIFY messages.
/// </summary>
public sealed record Server : DeviceInfo;

/// <summary>
/// The control point description sent in the <c>USER-AGENT</c> header of
/// M-SEARCH requests.
/// </summary>
public sealed record UserAgent : DeviceInfo;
