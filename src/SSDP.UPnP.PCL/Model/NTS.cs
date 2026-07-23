namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// The Notification Sub Type (NTS) of a NOTIFY message.
/// </summary>
public enum NTS
{
    /// <summary>The device or service is available (<c>ssdp:alive</c>).</summary>
    Alive,

    /// <summary>The device or service is leaving the network (<c>ssdp:byebye</c>).</summary>
    ByeBye,

    /// <summary>The device configuration changed (<c>ssdp:update</c>).</summary>
    Update,

    /// <summary>A UPnP eventing property change (<c>upnp:propchange</c>).</summary>
    Propchange,

    /// <summary>The NTS header was missing or not recognized.</summary>
    Unknown
}

/// <summary>
/// Functions over <see cref="NTS"/> values.
/// </summary>
public static class NTSExtensions
{
    /// <summary>
    /// Returns the SSDP wire representation of the notification sub type
    /// (e.g. <c>ssdp:alive</c>), or <c>&lt;unknown&gt;</c> when the value has none.
    /// </summary>
    public static string ToUriString(this NTS nts) => nts switch
    {
        NTS.Alive => "ssdp:alive",
        NTS.ByeBye => "ssdp:byebye",
        NTS.Update => "ssdp:update",
        NTS.Propchange => "upnp:propchange",
        _ => "<unknown>"
    };

    /// <summary>
    /// Parses the SSDP wire representation of a notification sub type;
    /// unrecognized values map to <see cref="NTS.Unknown"/>.
    /// </summary>
    public static NTS ToNTS(string? value) => value?.ToLowerInvariant() switch
    {
        "ssdp:alive" => NTS.Alive,
        "ssdp:byebye" => NTS.ByeBye,
        "ssdp:update" => NTS.Update,
        "upnp:propchange" => NTS.Propchange,
        _ => NTS.Unknown
    };
}
