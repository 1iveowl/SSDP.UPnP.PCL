using System.Net;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Model;

namespace SSDP.UPnP.PCL;

/// <summary>
/// An SSDP control point: sends M-SEARCH discovery requests and observes the
/// responses and NOTIFY advertisements on the local network.
/// </summary>
/// <remarks>
/// There is no start step: the observables below are cold until subscribed. The
/// first subscription starts listening, disposing the last subscription stops it,
/// and subscribing again restarts it.
/// </remarks>
public interface IControlPoint : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Supplies an externally created message stream to observe instead of this
    /// control point's own listeners — for advanced scenarios where the stream is
    /// shared with other services built on the same listener (UPnP eventing, for
    /// example, which is itself outside this library's scope).
    /// </summary>
    /// <remarks>
    /// Call before the first subscription; a control point accepts one stream only.
    /// </remarks>
    void HotStart(IObservable<HttpRequestResponse> httpListenerObservable);

    /// <summary>
    /// The NOTIFY advertisements (<c>ssdp:alive</c>, <c>ssdp:byebye</c>,
    /// <c>ssdp:update</c>) observed on the network. Listening starts on the first
    /// subscription and stops when the last subscription is disposed. The stream is
    /// shared: each message is parsed once regardless of subscriber count.
    /// </summary>
    IObservable<ReceivedNotify> NotifyObservable();

    /// <summary>
    /// The M-SEARCH responses observed on the network. Listening starts on the first
    /// subscription and stops when the last subscription is disposed. The stream is
    /// shared: each message is parsed once regardless of subscriber count.
    /// </summary>
    IObservable<ReceivedMSearchResponse> MSearchResponseObservable();

    /// <summary>
    /// The messages that arrived but could not be parsed as SSDP, with the reason.
    /// Diagnostic: these are the messages the streams above silently drop, which is
    /// otherwise invisible from the outside.
    /// </summary>
    /// <remarks>
    /// Enable raw capture on the control point to have the offending bytes carried
    /// on <see cref="SsdpParseFailure.RawMessage"/> as well. Subscribing to this
    /// alone starts listening, like any other stream here.
    /// </remarks>
    IObservable<SsdpParseFailure> ParseFailures();

    /// <summary>
    /// Sends an M-SEARCH request from the interface bound to
    /// <paramref name="ipAddress"/>: a <see cref="MulticastMSearch"/> goes to the
    /// SSDP group, a <see cref="UnicastMSearch"/> to its
    /// <see cref="UnicastMSearch.Target"/>.
    /// </summary>
    /// <remarks>
    /// Sending does not require an active subscription — but responses are only
    /// observed while one exists, so subscribe before searching.
    /// </remarks>
    Task SendMSearchAsync(MSearchRequest mSearch, IPAddress ipAddress, CancellationToken ct = default);
}
