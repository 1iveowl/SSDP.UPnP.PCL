using System.Net;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Model;

namespace SSDP.UPnP.PCL;

/// <summary>
/// An SSDP device: advertises a root device (and its embedded devices and
/// services) with NOTIFY messages and answers M-SEARCH requests.
/// </summary>
/// <remarks>
/// Prefer <c>await using</c>: <see cref="IAsyncDisposable.DisposeAsync"/> revokes
/// the device's advertisements with <c>ssdp:byebye</c> before releasing
/// resources, which is what UDA 2.0 section 1.2.3 asks of a device shutting down
/// gracefully. Plain <see cref="IDisposable.Dispose"/> releases resources only;
/// the advertisements then linger on the network until their
/// <c>CACHE-CONTROL</c> lifetime expires.
/// </remarks>
public interface IDevice : IDisposable, IAsyncDisposable
{
    /// <summary>Whether the device has been started.</summary>
    bool IsStarted { get; }

    /// <summary>The device's activity, e.g. for diagnostics or UI.</summary>
    IObservable<DeviceActivity> DeviceActivityObservable { get; }

    /// <summary>
    /// The M-SEARCH requests that arrived but could not be parsed, with the reason.
    /// </summary>
    /// <remarks>
    /// UDA 2.0 section 1.3.3 requires a device to discard a malformed search in
    /// silence, so this is the only way to see that it happened - and the answer to
    /// "why does my device ignore that control point?". Enable raw capture to have
    /// the offending bytes carried on <see cref="SsdpParseFailure.RawMessage"/>.
    /// </remarks>
    IObservable<SsdpParseFailure> ParseFailureObservable { get; }

    /// <summary>
    /// Starts listening for M-SEARCH requests on the configured interfaces and
    /// multicasts the initial <c>ssdp:alive</c> advertisements. Cancel
    /// <paramref name="ct"/> to stop listening. A device can only be started once.
    /// </summary>
    Task StartAsync(CancellationToken ct);

    /// <summary>
    /// Starts the device on an externally created message stream instead of its own
    /// listeners — for advanced scenarios where the stream is shared with other
    /// services built on the same listener (UPnP eventing, for example, which is
    /// itself outside this library's scope). A device can only be started once.
    /// </summary>
    Task HotStartAsync(IObservable<HttpRequestResponse> httpListenerObservable);

    /// <summary>
    /// Multicasts <c>ssdp:update</c> advertisements and advances the device's BOOTID,
    /// per UDA 2.0 section 1.2.4. Sends are best-effort; the BOOTID advance always
    /// takes effect.
    /// </summary>
    Task UpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// Multicasts <c>ssdp:byebye</c> advertisements for all devices and services.
    /// Call this before disposing for a clean exit — <see cref="IDisposable.Dispose"/>
    /// does not notify the network.
    /// </summary>
    Task ByeByeAsync(CancellationToken ct = default);

    /// <summary>
    /// Multicasts a custom NOTIFY message from the interface bound to
    /// <paramref name="ipEndPoint"/>.
    /// </summary>
    Task SendNotifyAsync(Notify notify, IPEndPoint ipEndPoint, CancellationToken ct = default);
}
