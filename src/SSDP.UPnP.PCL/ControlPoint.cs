using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using SimpleHttpListener.Rx;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Model;
using SSDP.UPnP.PCL.Parsing;

namespace SSDP.UPnP.PCL;

/// <summary>
/// An SSDP control point: sends M-SEARCH discovery requests and observes the
/// responses and NOTIFY advertisements on the local network. Supports
/// multi-homed operation by listening on several interfaces at once.
/// IPv4 only.
/// </summary>
public class ControlPoint : IControlPoint
{
    private readonly IReadOnlyList<ControlPointInterface> _controlPointInterfaces;

    private readonly bool _isClientsProvided;

    private IObservable<MSearchResponse>? _mSearchResponseObservable;

    private IObservable<Notify>? _notifyObservable;

    /// <summary>
    /// The time source used for the delay between repeated M-SEARCH transmissions;
    /// replace with a fake in tests.
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <inheritdoc />
    public bool IsStarted { get; private set; }

    /// <summary>
    /// Creates a control point that listens on the given local IP addresses, with
    /// the default TCP response port (<see cref="Constants.TcpResponseListenerPort"/>).
    /// Each address gets a UDP client joined to the SSDP multicast group and a TCP
    /// listener for unicast responses; more than one address creates a multi-homed
    /// control point.
    /// </summary>
    /// <param name="ipAddressParam">One or more local IPv4 addresses to bind.</param>
    /// <exception cref="SSDPException">No address was given, or an address could not be tied to a network interface.</exception>
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
    /// <exception cref="SSDPException">No address was given, or an address could not be tied to a network interface.</exception>
    public ControlPoint(IEnumerable<IPAddress> ipAddresses, int tcpResponsePort, int multicastTtl = Constants.DefaultMulticastTtl)
    {
        var addresses = ipAddresses?.ToList();

        if (addresses is null || addresses.Count == 0)
        {
            throw new SSDPException("At least one IP Address must be specified");
        }

        _controlPointInterfaces = addresses
            .Select(ipAddress => CreateInterface(ipAddress, tcpResponsePort, multicastTtl))
            .ToList();
    }

    /// <summary>
    /// Creates a control point from prepared interfaces — for advanced scenarios
    /// where the caller configures the sockets. The caller keeps ownership of the
    /// sockets; <see cref="Dispose"/> will not close them.
    /// </summary>
    /// <param name="controlPointInterfaceParams">One or more prepared interfaces.</param>
    /// <exception cref="SSDPException">No interface was given.</exception>
    public ControlPoint(params ControlPointInterface[] controlPointInterfaceParams)
    {
        if (controlPointInterfaceParams is null || controlPointInterfaceParams.Length == 0)
        {
            throw new SSDPException("At least one Control Point Interface must be specified.");
        }

        _controlPointInterfaces = controlPointInterfaceParams;

        _isClientsProvided = true;
    }

    private static ControlPointInterface CreateInterface(IPAddress ipAddress, int tcpResponsePort, int multicastTtl)
    {
        var udpClient = new UdpClient();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            udpClient.ExclusiveAddressUse = false;
            udpClient.MulticastLoopback = true;
        }

        var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(nic =>
                nic.GetIPProperties().UnicastAddresses.Any(addr => Equals(addr.Address, ipAddress)))
            ?? throw new SSDPException("Unable to tie IPAddress to network interface.");

        var optionValue = IPAddress.NetworkToHostOrder(networkInterface.GetIPProperties().GetIPv4Properties().Index);

        udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, optionValue);
        udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, multicastTtl);
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpClient.Client.Bind(new IPEndPoint(ipAddress, Constants.UdpSSDPMulticastPort));
        udpClient.JoinMulticastGroup(IPAddress.Parse(Constants.UdpSSDPMultiCastAddress), ipAddress);

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

    /// <inheritdoc />
    /// <exception cref="SSDPException">
    /// The control point is already started, or an interface has neither a UDP
    /// client nor a TCP listener.
    /// </exception>
    public void Start(CancellationToken ct)
    {
        if (IsStarted)
        {
            throw new SSDPException("Control Point is already started.");
        }

        var listenerObservables = new List<IObservable<HttpRequestResponse>>();

        foreach (var node in _controlPointInterfaces)
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

        BuildParsedStreams(listenerObservables.Merge().Publish().RefCount());
    }

    /// <inheritdoc />
    /// <exception cref="SSDPException">The control point is already started.</exception>
    public void HotStart(IObservable<HttpRequestResponse> httpListenerObservable)
    {
        if (IsStarted)
        {
            throw new SSDPException("Control Point is already started.");
        }

        BuildParsedStreams(httpListenerObservable);
    }

    // The parsed streams are built once and shared (Publish/RefCount), so any
    // number of subscribers cause each message to be parsed exactly once.
    private void BuildParsedStreams(IObservable<HttpRequestResponse> source)
    {
        _mSearchResponseObservable = source
            .Where(x => x.MessageType == MessageType.Response)
            .Select(SsdpMessageParser.ParseMSearchResponse)
            .Where(result => result.IsSuccess)
            .Select(result => result.Value!)
            .Publish()
            .RefCount();

        _notifyObservable = source
            .Where(x => x.MessageType == MessageType.Request)
            .Where(req => req.Method == "NOTIFY")
            .Select(SsdpMessageParser.ParseNotify)
            .Where(result => result.IsSuccess)
            .Select(result => result.Value!)
            .Where(notify => notify.NTS is NTS.Alive or NTS.ByeBye or NTS.Update)
            .Publish()
            .RefCount();

        IsStarted = true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Messages that fail SSDP parsing are dropped. To observe raw traffic or parse
    /// failures, use <see cref="HotStart"/> with your own listener and
    /// <see cref="SsdpMessageParser"/>.
    /// </remarks>
    /// <exception cref="SSDPException">The control point has not been started.</exception>
    public IObservable<MSearchResponse> MSearchResponseObservable() =>
        _mSearchResponseObservable ?? throw new SSDPException("Control Point not started.");

    /// <inheritdoc />
    /// <remarks>
    /// Only <c>ssdp:alive</c>, <c>ssdp:byebye</c> and <c>ssdp:update</c>
    /// notifications are emitted; other or unparsable messages are dropped.
    /// </remarks>
    /// <exception cref="SSDPException">The control point has not been started.</exception>
    public IObservable<Notify> NotifyObservable() =>
        _notifyObservable ?? throw new SSDPException("Control Point not started.");

    /// <inheritdoc />
    /// <exception cref="SSDPException">
    /// The control point has not been started, <paramref name="ipAddress"/> is not
    /// one of its interfaces, or the request is not fully specified.
    /// </exception>
    public async Task SendMSearchAsync(MSearchRequest mSearch, IPAddress ipAddress, CancellationToken ct = default)
    {
        if (!IsStarted)
        {
            throw new SSDPException("Control Point not started.");
        }

        ArgumentNullException.ThrowIfNull(mSearch);

        var cp = _controlPointInterfaces.FirstOrDefault(c => Equals(c.IpAddress, ipAddress));

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
                        await Task.Delay(TimeSpan.FromMilliseconds(100), TimeProvider, ct);
                    }

                    await cp.UdpClient.SendAsync(
                        dataGram,
                        new IPEndPoint(IPAddress.Parse(Constants.UdpSSDPMultiCastAddress), Constants.UdpSSDPMulticastPort),
                        ct);
                }

                break;
            case TransportType.Unicast when mSearch.RemoteIpEndPoint is not null:
                await SendOnTcpAsync(mSearch.RemoteIpEndPoint, dataGram, ct);
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

        await tcpClient.ConnectAsync(ipEndPoint.Address, ipEndPoint.Port, ct);

        var stream = tcpClient.GetStream();

        await stream.WriteAsync(data, ct);
        await stream.FlushAsync(ct);
    }

    /// <summary>
    /// Closes the sockets this control point created. Sockets supplied through the
    /// prepared-interface constructor are left open, since the caller owns them.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (_isClientsProvided)
        {
            return;
        }

        foreach (var client in _controlPointInterfaces)
        {
            client.UdpClient?.Dispose();
            client.TcpListener?.Dispose();
        }
    }
}
