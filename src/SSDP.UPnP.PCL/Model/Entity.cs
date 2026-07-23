namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// A UPnP entity: a device or service identity as used in search targets,
/// notification types and USNs. Immutable.
/// </summary>
public record Entity
{
    /// <summary>The kind of entity.</summary>
    public EntityType EntityType { get; init; }

    /// <summary>Device or service type name (the <c>[type]</c> part of a type URN).</summary>
    public string? TypeName { get; init; }

    /// <summary>Type version; UPnP versions start at 1.</summary>
    public int Version { get; init; }

    /// <summary>Vendor domain for domain-qualified types; <see langword="null"/> for standard types.</summary>
    public string? Domain { get; init; }

    /// <summary>UUID of the device (or of the device owning the service).</summary>
    public string? DeviceUUID { get; init; }

    /// <summary>
    /// The SSDP URI of this entity as used in <c>NT</c> and <c>ST</c> headers, e.g.
    /// <c>upnp:rootdevice</c>, <c>uuid:[UUID]</c> or
    /// <c>urn:schemas-upnp-org:device:[type]:[version]</c>.
    /// </summary>
    /// <exception cref="SSDPException">The entity is not fully specified for its <see cref="EntityType"/>.</exception>
    public string ToUriString() => SsdpUri.For(EntityType, DeviceUUID, Domain, TypeName, Version);
}

/// <summary>
/// Composes SSDP entity URIs.
/// </summary>
internal static class SsdpUri
{
    internal static string For(EntityType entityType, string? deviceUuid, string? domain, string? typeName, int version) =>
        entityType switch
        {
            EntityType.RootDevice => "upnp:rootdevice",
            EntityType.Device => $"uuid:{deviceUuid}",
            EntityType.DeviceType => $"urn:schemas-upnp-org:device:{typeName}:{version}",
            EntityType.ServiceType => $"urn:schemas-upnp-org:service:{typeName}:{version}",
            EntityType.DomainDevice => $"urn:{domain}:device:{typeName}:{version}",
            EntityType.DomainService => $"urn:{domain}:service:{typeName}:{version}",
            _ => throw new SSDPException($"Unknown entity type: {entityType}.")
        };
}
