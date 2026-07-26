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

    /// <summary>
    /// UPnP architecture major version, or <see langword="null"/> when the sender
    /// declared no <c>UPnP/</c> token. Absence is reported rather than assumed:
    /// a message that said nothing about its architecture version is not evidence
    /// of any particular one.
    /// </summary>
    public int? UpnpMajorVersion { get; init; }

    /// <summary>
    /// UPnP architecture minor version, or <see langword="null"/> when the sender
    /// declared no <c>UPnP/</c> token.
    /// </summary>
    public int? UpnpMinorVersion { get; init; }

    /// <summary>Whether the sender declared UPnP 2.x support.</summary>
    /// <remarks>
    /// Misleading for version checks: it is false for UDA 1.1 devices, which are
    /// not 1.0 and do send <c>BOOTID.UPNP.ORG</c>. Prefer
    /// <see cref="SupportsAtLeast"/>, or compare
    /// <see cref="UpnpMajorVersion"/>/<see cref="UpnpMinorVersion"/> directly.
    /// </remarks>
    [Obsolete("Use SupportsAtLeast, or compare UpnpMajorVersion/UpnpMinorVersion. " +
              "IsUpnp2 is false for UDA 1.1 senders, which is rarely the question being asked.")]
    public bool IsUpnp2 => UpnpMajorVersion >= 2;

    /// <summary>
    /// Whether the sender declared an architecture version of at least
    /// <paramref name="major"/>.<paramref name="minor"/>. A sender that declared no
    /// version at all returns <see langword="false"/>.
    /// </summary>
    /// <example>
    /// <c>server.SupportsAtLeast(1, 1)</c> answers "does this sender speak UDA 1.1
    /// or later", which is the question behind features such as
    /// <c>BOOTID.UPNP.ORG</c>.
    /// </example>
    public bool SupportsAtLeast(int major, int minor = 0) =>
        UpnpMajorVersion is { } declaredMajor
        && (declaredMajor > major
            || (declaredMajor == major && (UpnpMinorVersion ?? 0) >= minor));

    /// <summary>
    /// The SSDP wire representation:
    /// <c>OS/version UPnP/major.minor product/version</c>.
    /// </summary>
    /// <remarks>
    /// This library implements UDA 2.0, so an unset architecture version is sent
    /// as <c>UPnP/2.0</c>: UDA 2.0 section 1.1.2 requires the token, and omitting
    /// it would produce a non-conforming header.
    /// </remarks>
    public string ToHeaderString() =>
        $"{OperatingSystem}/{OperatingSystemVersion} " +
        $"UPnP/{UpnpMajorVersion ?? 2}.{UpnpMinorVersion ?? 0} " +
        $"{ProductName}/{ProductVersion}";
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
