using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reactive.Linq;
using SimpleHttpListener.Rx;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Internal;
using SSDP.UPnP.PCL.Model;
using SSDP.UPnP.PCL.Parsing;

namespace SSDP.UPnP.PCL;

/// <summary>
/// An SSDP control point: sends M-SEARCH discovery requests and observes the
/// responses and NOTIFY advertisements on the local network. Supports
/// multi-homed operation by listening on several interfaces at once.
/// IPv4 only.
/// </summary>
/// <remarks>
/// <para>
/// The control point has no start step. Its observables are cold until
/// subscribed: the first subscription binds the sockets (if they are not bound
/// already) and begins listening; disposing the last subscription stops
/// listening. Subscribing again restarts listening on the same sockets.
/// </para>
/// <para>
/// Sockets are created once, on first use — the first subscription or the first
/// <see cref="SendMSearchAsync"/> — and live until <see cref="Dispose"/>, which
/// is what closes them. Construction itself binds nothing.
/// </para>
/// </remarks>
public class ControlPoint : IControlPoint
{
    // Created once on first use (subscription or send) and reused for the object's
    // lifetime, so that listening can stop and restart without rebinding, and so
    // that sending works independently of whether anyone is subscribed.
    private readonly Lazy<IReadOnlyList<ControlPointInterface>> _controlPointInterfaces;

    private readonly bool _isClientsProvided;

    // Stops any live listening when the control point itself is disposed — the
    // sockets outlive individual subscriptions, so their teardown needs its own
    // signal. Individual subscriptions stop by being disposed.
    private readonly CancellationTokenSource _lifetimeCts = new();

    private readonly IObservable<MSearchResponse> _mSearchResponseObservable;

    private readonly IObservable<Notify> _notifyObservable;

    private IObservable<HttpRequestResponse>? _hotSource;

    private bool _disposed;

    /// <summary>
    /// The time source used for the delay between repeated M-SEARCH transmissions;
    /// replace with a fake in tests.
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>
    /// Creates a control point that listens on the given local IP addresses, with
    /// the default TCP response port (<see cref="Constants.TcpResponseListenerPort"/>).
    /// Each address gets a UDP client joined to the SSDP multicast group and a TCP
    /// listener for unicast responses; more than one address creates a multi-homed
    /// control point.
    /// </summary>
    /// <remarks>
    /// The sockets are bound on first use, not here; an address that cannot be tied
    /// to a network interface therefore surfaces as an error on the first
    /// subscription or send, not from the constructor.
    /// </remarks>
    /// <param name="ipAddressParam">One or more local IPv4 addresses to bind.</param>
    /// <exception cref="SSDPException">No address was given.</exception>
    public ControlPoint(params IPAddress[] ipAddressParam)
        : this(ipAddressParam, Constants.TcpResponseListenerPort)
    {
    }

    /// <summary>
    /// Creates a control point that listens on the given local IP addresses, using
    /// <paramref name="tcpResponsePort"/> for the per-interface TCP response
    /// listener — use distinct ports to run several control points on one host.
    /// </summary>
    /// <param name="ipAddresses">One or more local IPv4 addresses to bind.</param>
    /// <param name="tcpResponsePort">
    /// The local TCP port to listen on for TCP responses; must be in 49152–65535 to
    /// be usable as <c>TCPPORT.UPNP.ORG</c>.
    /// </param>
    /// <param name="multicastTtl">
    /// Time-to-live for multicast packets; UDA 2.0 recommends the default of 2.
    /// </param>
    /// <exception cref="SSDPException">No address was given.</exception>
    public ControlPoint(IEnumerable<IPAddress> ipAddresses, int tcpResponsePort, int multicastTtl = Constants.DefaultMulticastTtl)
        : this(CreateInterfaceFactory(ipAddresses, tcpResponsePort, multicastTtl), isClientsProvided: false)
    {
    }

    /// <summary>
    /// Creates a control point from prepared interfaces — for advanced scenarios
    /// where the caller configures the sockets. The caller keeps ownership of the
    /// sockets; <see cref="Dispose"/> will not close them.
    /// </summary>
    /// <param name="controlPointInterfaceParams">One or more prepared interfaces.</param>
    /// <exception cref="SSDPException">No interface was given.</exception>
    public ControlPoint(params ControlPointInterface[] controlPointInterfaceParams)
        : this(
            ValidatedFactory(controlPointInterfaceParams),
            isClientsProvided: true)
    {
    }

    // Test seam: lets tests count how often (and when) the sockets are materialized.
    internal ControlPoint(Func<IReadOnlyList<ControlPointInterface>> interfaceFactory, bool isClientsProvided)
    {
        _controlPointInterfaces = new Lazy<IReadOnlyList<ControlPointInterface>>(interfaceFactory);
        _isClientsProvided = isClientsProvided;

        // One shared message source for both parsed streams: the first parsed
        // stream to be subscribed brings the listeners up, the last one to go
        // takes them down, and both see the same messages in between.
        var messageSource = Observable
            .Defer(CreateMessageSource)
            .Publish()
            .RefCount();

        // Each parsed stream is shared too, so any number of subscribers to it
        // cause each message to be parsed exactly once.
        _mSearchResponseObservable = messageSource
            .Where(x => x.MessageType == MessageType.Response)
            .Select(SsdpMessageParser.ParseMSearchResponse)
            .Where(result => result.IsSuccess)
            .Select(result => result.Value!)
            .Publish()
            .RefCount();

        _notifyObservable = messageSource
            .Where(x => x.MessageType == MessageType.Request)
            .Where(req => req.Method == "NOTIFY")
            .Select(SsdpMessageParser.ParseNotify)
            .Where(result => result.IsSuccess)
            .Select(result => result.Value!)
            .Where(notify => notify.NTS is NTS.Alive or NTS.ByeBye or NTS.Update)
            .Publish()
            .RefCount();
    }

    /// <summary>Whether the sockets have been materialized (test diagnostics).</summary>
    internal bool InterfacesMaterialized => _controlPointInterfaces.IsValueCreated;

    private static Func<IReadOnlyList<ControlPointInterface>> ValidatedFactory(
        ControlPointInterface[] controlPointInterfaceParams)
    {
        if (controlPointInterfaceParams is null || controlPointInterfaceParams.Length == 0)
        {
            throw new SSDPException("At least one Control Point Interface must be specified.");
        }

        return () => controlPointInterfaceParams;
    }

    private static Func<IReadOnlyList<ControlPointInterface>> CreateInterfaceFactory(
        IEnumerable<IPAddress> ipAddresses,
        int tcpResponsePort,
        int multicastTtl)
    {
        var addresses = ipAddresses?.ToList();

        if (addresses is null || addresses.Count == 0)
        {
            throw new SSDPException("At least one IP Address must be specified");
        }

        return () => addresses
            .Select(ipAddress => CreateInterface(ipAddress, tcpResponsePort, multicastTtl))
            .ToList();
    }

    private static ControlPointInterface CreateInterface(IPAddress ipAddress, int tcpResponsePort, int multicastTtl)
    {
        var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(nic =>
                nic.GetIPProperties().UnicastAddresses.Any(addr => Equals(addr.Address, ipAddress)))
            ?? throw new SSDPException("Unable to tie IPAddress to network interface.");

        var udpClient = MulticastSocket.CreateJoined(ipAddress, Constants.UdpSSDPMulticastPort, multicastTtl);

        // Send multicast out of this interface specifically, so a multi-homed
        // control point searches on the interface it was configured with.
        var optionValue = IPAddress.NetworkToHostOrder(networkInterface.GetIPProperties().GetIPv4Properties().Index);

        udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, optionValue);

        return new ControlPointInterface
        {
            IpAddress = ipAddress,
            UdpClient = udpClient,
            TcpListener = new TcpListener(new IPEndPoint(ipAddress, tcpResponsePort))
            {
                ExclusiveAddressUse = false
            }
        };
    }

    // Invoked on every 0 -> 1 subscriber transition: materializes the sockets on
    // the first one, then builds fresh listener observables over them. Listening
    // restarts cleanly on re-subscription (requires SimpleHttpListener.Rx 7.3.0
    // or later, which tolerates dispose-then-resubscribe).
    private IObservable<HttpRequestResponse> CreateMessageSource()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_hotSource is not null)
        {
            return _hotSource;
        }

        var ct = _lifetimeCts.Token;

        var listenerObservables = new List<IObservable<HttpRequestResponse>>();

        foreach (var node in _controlPointInterfaces.Value)
        {
            if (node.UdpClient is null && node.TcpListener is null)
            {
                throw new SSDPException("No network UDP Client or TCP Listener defined for Control Point Interface");
            }

            if (node.UdpClient is not null)
            {
                listenerObservables.Add(
                    node.UdpClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError));
            }

            if (node.TcpListener is not null)
            {
                listenerObservables.Add(
                    node.TcpListener.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError));
            }
        }

        return listenerObservables.Merge();
    }

    /// <inheritdoc />
    /// <exception cref="SSDPException">A message stream has already been supplied.</exception>
    public void HotStart(IObservable<HttpRequestResponse> httpListenerObservable)
    {
        ArgumentNullException.ThrowIfNull(httpListenerObservable);

        if (_hotSource is not null)
        {
            throw new SSDPException("A message stream has already been supplied to this Control Point.");
        }

        _hotSource = httpListenerObservable;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Messages that fail SSDP parsing are dropped. To observe raw traffic or parse
    /// failures, use <see cref="HotStart"/> with your own listener and
    /// <see cref="SsdpMessageParser"/>.
    /// </remarks>
    public IObservable<MSearchResponse> MSearchResponseObservable() => _mSearchResponseObservable;

    /// <inheritdoc />
    /// <remarks>
    /// Only <c>ssdp:alive</c>, <c>ssdp:byebye</c> and <c>ssdp:update</c>
    /// notifications are emitted; other or unparsable messages are dropped.
    /// </remarks>
    public IObservable<Notify> NotifyObservable() => _notifyObservable;

    /// <inheritdoc />
    /// <exception cref="SSDPException">
    /// <paramref name="ipAddress"/> is not one of this control point's interfaces,
    /// or the request is not fully specified.
    /// </exception>
    public async Task SendMSearchAsync(MSearchRequest mSearch, IPAddress ipAddress, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(mSearch);

        // Sending materializes the sockets if nothing has yet: a search is useful
        // on its own (responses can be observed by another control point, or the
        // caller may subscribe afterwards), so there is no start to get wrong.
        // Note that responses arriving before a subscription exists are not
        // buffered — subscribe first if you want to see them.
        var cp = _controlPointInterfaces.Value.FirstOrDefault(c => Equals(c.IpAddress, ipAddress));

        if (cp?.UdpClient is null)
        {
            throw new SSDPException("IP Address provided is not associated with any ControlPoint EndPoint or no Control Points specified.");
        }

        var dataGram = DatagramComposer.ComposeMSearchRequest(mSearch);

        switch (mSearch.TransportType)
        {
            case TransportType.Multicast:
                // UDA 2.0 section 1.3.2: control points should send each M-SEARCH
                // more than once, since UDP is unreliable.
                var sendCount = Math.Max(1, mSearch.SendCount);

                for (var i = 0; i < sendCount; i++)
                {
                    if (i > 0)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100), TimeProvider, ct).ConfigureAwait(false);
                    }

                    await cp.UdpClient.SendAsync(
                        dataGram,
                        new IPEndPoint(IPAddress.Parse(Constants.UdpSSDPMultiCastAddress), Constants.UdpSSDPMulticastPort),
                        ct).ConfigureAwait(false);
                }

                break;
            case TransportType.Unicast when mSearch.RemoteIpEndPoint is not null:
                await SendOnTcpAsync(mSearch.RemoteIpEndPoint, dataGram, ct).ConfigureAwait(false);
                break;
            case TransportType.Unicast:
                throw new SSDPException("A unicast M-SEARCH requires a RemoteIpEndPoint.");
            default:
                throw new SSDPException("M-SEARCH must be either multicast or unicast.");
        }
    }

    private static async Task SendOnTcpAsync(IPEndPoint ipEndPoint, byte[] data, CancellationToken ct)
    {
        using var tcpClient = new TcpClient();

        await tcpClient.ConnectAsync(ipEndPoint.Address, ipEndPoint.Port, ct).ConfigureAwait(false);

        var stream = tcpClient.GetStream();

        await stream.WriteAsync(data, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Stops any live listening and closes the sockets this control point created.
    /// </summary>
    /// <remarks>
    /// A control point advertises nothing, so it owes the network no goodbye and
    /// this does the same work as <see cref="Dispose"/>. It exists so consumers can
    /// <c>await using</c> a control point and a device alike.
    /// </remarks>
    public ValueTask DisposeAsync()
    {
        Dispose();

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Stops any live listening and closes the sockets this control point created.
    /// Sockets supplied through the prepared-interface constructor are left open,
    /// since the caller owns them. Safe to call more than once.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _lifetimeCts.Cancel();
        _lifetimeCts.Dispose();

        if (_isClientsProvided || !_controlPointInterfaces.IsValueCreated)
        {
            return;
        }

        foreach (var client in _controlPointInterfaces.Value)
        {
            client.UdpClient?.Dispose();
            client.TcpListener?.Dispose();
        }
    }
}
