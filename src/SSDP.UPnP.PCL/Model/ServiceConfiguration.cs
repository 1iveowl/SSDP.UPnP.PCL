namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// The configuration of a service hosted by a device. Immutable.
/// </summary>
/// <remarks>
/// The advertised URI form (standard vs vendor-domain service type) is derived
/// from <see cref="Entity.Domain"/>; <see cref="Entity.EntityType"/> is not used
/// for configurations.
/// </remarks>
public sealed record ServiceConfiguration : Entity;
