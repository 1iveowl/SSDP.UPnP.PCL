namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// Activity states emitted by <see cref="SSDP.UPnP.PCL.IDevice.DeviceActivityObservable"/>.
/// </summary>
public enum DeviceActivity
{
    /// <summary>The device has been created but has not sent or received anything yet.</summary>
    Initialized,

    /// <summary>The device is responding to an M-SEARCH request.</summary>
    Responding,

    /// <summary>The device is multicasting a NOTIFY message.</summary>
    Notifying
}
