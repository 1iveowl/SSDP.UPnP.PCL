namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// The configuration of a UPnP device (root or embedded). Immutable; use
/// <c>with</c> expressions to derive updated copies.
/// </summary>
public record DeviceConfiguration : Entity
{
    /// <summary>
    /// The boot instance id sent as <c>BOOTID.UPNP.ORG</c>; defaults to the Unix
    /// timestamp at creation, per UDA 2.0 section 1.2.2.
    /// </summary>
    public uint BOOTID { get; init; } = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>The services hosted by this device.</summary>
    public IReadOnlyList<ServiceConfiguration> Services { get; init; } = [];
}
