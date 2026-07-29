namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// The kind of UPnP entity a description, search target or notification refers to.
/// </summary>
public enum EntityType
{
    /// <summary>A specific device, addressed by its UUID.</summary>
    Device,

    /// <summary>The root device of a device tree (<c>upnp:rootdevice</c>).</summary>
    RootDevice,

    /// <summary>A standard device type (<c>urn:schemas-upnp-org:device:...</c>).</summary>
    DeviceType,

    /// <summary>A standard service type (<c>urn:schemas-upnp-org:service:...</c>).</summary>
    ServiceType,

    /// <summary>A vendor-domain device type (<c>urn:[domain]:device:...</c>).</summary>
    DomainDevice,

    /// <summary>A vendor-domain service type (<c>urn:[domain]:service:...</c>).</summary>
    DomainService,

    /// <summary>
    /// The entity part of a received <c>USN</c> could not be parsed, so what it
    /// identified is unknown - but the device UUID in front of it was fine.
    /// </summary>
    /// <remarks>
    /// Only ever produced by <see cref="USN.Parse"/>, and never composable: a
    /// message this library sends cannot advertise an entity it does not
    /// understand, so <see cref="Entity.ToUriString"/> throws for it.
    /// <para>
    /// Distinct from <see cref="Device"/> on purpose. A bare <c>uuid:[id]</c> USN
    /// means the device is advertising itself, which is a real statement; this
    /// means the sender said something and we could not read it. Reporting the
    /// second as the first would be a lie about what is on the network.
    /// </para>
    /// </remarks>
    Unknown
}
