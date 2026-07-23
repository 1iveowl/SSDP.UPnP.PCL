using System.Net;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Model;

namespace SSDP.UPnP.PCL;

/// <summary>
/// An SSDP control point: sends M-SEARCH discovery requests and observes the
/// responses and NOTIFY advertisements on the local network.
/// </summary>
public interface IControlPoint : IDisposable
{
    /// <summary>Whether the control point has been started and its observables are available.</summary>
    bool IsStarted { get; }

    /// <summary>
    /// Creates the network listeners for the configured interfaces and starts
    /// listening. Cancel <paramref name="ct"/> to stop.
    /// </summary>
    void Start(CancellationToken ct);

    /// <summary>
    /// Starts the control point on an externally created message stream instead of
    /// its own listeners — for advanced scenarios where the stream is shared with
    /// other services (e.g. UPnP eventing).
    /// </summary>
    void HotStart(IObservable<HttpRequestResponse> httpListenerObservable);

    /// <summary>
    /// The NOTIFY advertisements (<c>ssdp:alive</c>, <c>ssdp:byebye</c>,
    /// <c>ssdp:update</c>) observed on the network.
    /// </summary>
    IObservable<Notify> NotifyObservable();

    /// <summary>
    /// The M-SEARCH responses observed on the network.
    /// </summary>
    IObservable<MSearchResponse> MSearchResponseObservable();

    /// <summary>
    /// Sends an M-SEARCH request from the interface bound to
    /// <paramref name="ipAddress"/>: multicast to the SSDP group, or unicast to
    /// <see cref="MSearchRequest.RemoteIpEndPoint"/>.
    /// </summary>
    Task SendMSearchAsync(MSearchRequest mSearch, IPAddress ipAddress);
}
