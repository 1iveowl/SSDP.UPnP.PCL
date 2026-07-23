using System.Net;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Model;

namespace SSDP.UPnP.PCL;

/// <summary>
/// An SSDP device: advertises a root device (and its embedded devices and
/// services) with NOTIFY messages and answers M-SEARCH requests.
/// </summary>
public interface IDevice : IDisposable
{
    /// <summary>Whether the device has been started.</summary>
    bool IsStarted { get; }

    /// <summary>The device's activity, e.g. for diagnostics or UI.</summary>
    IObservable<DeviceActivity> DeviceActivityObservable { get; }

    /// <summary>
    /// Starts listening for M-SEARCH requests on the configured interfaces and
    /// multicasts the initial <c>ssdp:alive</c> advertisements. Cancel
    /// <paramref name="ct"/> to stop listening.
    /// </summary>
    Task StartAsync(CancellationToken ct);

    /// <summary>
    /// Starts the device on an externally created message stream instead of its own
    /// listeners — for advanced scenarios where the stream is shared with other
    /// services (e.g. UPnP eventing).
    /// </summary>
    Task HotStartAsync(IObservable<HttpRequestResponse> httpListenerObservable);

    /// <summary>
    /// Multicasts <c>ssdp:update</c> advertisements and advances the device's BOOTID,
    /// per UDA 2.0 section 1.2.4.
    /// </summary>
    Task UpdateAsync();

    /// <summary>
    /// Multicasts <c>ssdp:byebye</c> advertisements for all devices and services.
    /// </summary>
    Task ByeByeAsync();

    /// <summary>
    /// Multicasts a custom NOTIFY message from the interface bound to
    /// <paramref name="ipEndPoint"/>.
    /// </summary>
    Task SendNotifyAsync(Notify notify, IPEndPoint ipEndPoint);
}
