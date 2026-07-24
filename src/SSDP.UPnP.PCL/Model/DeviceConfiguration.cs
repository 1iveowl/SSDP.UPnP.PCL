namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// The configuration of a UPnP device (root or embedded). Immutable; use
/// <c>with</c> expressions to derive updated copies.
/// </summary>
/// <remarks>
/// The advertised URI forms (root device, device UUID, standard or vendor-domain
/// device type) are derived from the device tree structure and <see cref="Entity.Domain"/>;
/// <see cref="Entity.EntityType"/> is not used for configurations.
/// </remarks>
public record DeviceConfiguration : Entity
{
    /// <summary>
    /// The boot instance id sent as <c>BOOTID.UPNP.ORG</c>. Leave at 0 (the
    /// default) to have <see cref="Device"/> stamp it with the Unix timestamp at
    /// start, per UDA 2.0 section 1.2.2; set explicitly to control it yourself.
    /// </summary>
    public uint BOOTID { get; init; }

    /// <summary>The services hosted by this device.</summary>
    public IReadOnlyList<ServiceConfiguration> Services { get; init; } = [];
}
