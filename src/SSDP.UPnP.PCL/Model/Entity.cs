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

    /// <summary>
    /// Vendor domain for domain-qualified types; <see langword="null"/> for standard
    /// types. Per UDA 2.0, period characters in the domain name must be replaced
    /// with hyphens (e.g. <c>acme-com</c>, not <c>acme.com</c>).
    /// </summary>
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
    // Interpolating an unset field produced a syntactically valid but meaningless
    // URI - "uuid:", or "urn:schemas-upnp-org:service::0" - which then went out on
    // the wire as a Required NT or USN header. ST.ToSearchTargetString has always
    // rejected the same shapes; this brings the other half of the model in line.
    internal static string For(EntityType entityType, string? deviceUuid, string? domain, string? typeName, int version)
    {
        switch (entityType)
        {
            case EntityType.RootDevice:
                return "upnp:rootdevice";

            case EntityType.Device:
                RequireUuid(deviceUuid, entityType);

                return $"uuid:{deviceUuid}";

            case EntityType.DeviceType:
            case EntityType.ServiceType:
            case EntityType.DomainDevice:
            case EntityType.DomainService:
                if (string.IsNullOrEmpty(typeName))
                {
                    throw new SSDPException($"{entityType} requires a Type name to be specified.");
                }

                if (version < 1)
                {
                    throw new SSDPException($"{entityType} requires a version (1 or greater) to be specified.");
                }

                var isDomainForm = entityType is EntityType.DomainDevice or EntityType.DomainService;

                if (isDomainForm && string.IsNullOrEmpty(domain))
                {
                    throw new SSDPException($"{entityType} requires a Domain to be specified.");
                }

                var resolvedDomain = isDomainForm ? domain : "schemas-upnp-org";
                var kind = entityType is EntityType.DeviceType or EntityType.DomainDevice ? "device" : "service";

                return $"urn:{resolvedDomain}:{kind}:{typeName}:{version}";

            default:
                throw new SSDPException($"Unknown entity type: {entityType}.");
        }
    }

    internal static void RequireUuid(string? deviceUuid, EntityType entityType)
    {
        if (string.IsNullOrEmpty(deviceUuid))
        {
            throw new SSDPException($"{entityType} requires a Device UUID to be specified.");
        }
    }
}
