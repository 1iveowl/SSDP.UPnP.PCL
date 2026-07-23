namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// The kind of Search Target (ST) of an M-SEARCH request, per UPnP Device
/// Architecture 2.0 section 1.3.2.
/// </summary>
public enum STType
{
    /// <summary>Search for everything (<c>ssdp:all</c>).</summary>
    All,

    /// <summary>Search for root devices only (<c>upnp:rootdevice</c>).</summary>
    RootDeviceSearch,

    /// <summary>Search for a specific device by UUID (<c>uuid:[device-UUID]</c>).</summary>
    UuidSearch,

    /// <summary>Search for a standard device type (<c>urn:schemas-upnp-org:device:[type]:[version]</c>).</summary>
    DeviceTypeSearch,

    /// <summary>Search for a standard service type (<c>urn:schemas-upnp-org:service:[type]:[version]</c>).</summary>
    ServiceTypeSearch,

    /// <summary>Search for a vendor-domain device type (<c>urn:[domain]:device:[type]:[version]</c>).</summary>
    DomainDeviceSearch,

    /// <summary>Search for a vendor-domain service type (<c>urn:[domain]:service:[type]:[version]</c>).</summary>
    DomainServiceSearch
}
