namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// How an SSDP message is (or was) transported.
/// </summary>
public enum TransportType
{
    /// <summary>The transport is unknown or not applicable.</summary>
    NoCast,

    /// <summary>UDP multicast to the SSDP group (239.255.255.250:1900).</summary>
    Multicast,

    /// <summary>Unicast to a specific endpoint.</summary>
    Unicast
}
