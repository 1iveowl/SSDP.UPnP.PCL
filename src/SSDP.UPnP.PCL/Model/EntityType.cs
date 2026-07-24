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
    DomainService
}
