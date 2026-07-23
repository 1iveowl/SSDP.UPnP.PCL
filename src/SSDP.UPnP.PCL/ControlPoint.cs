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
/// </summary>
public class ControlPoint : IControlPoint
{
    private readonly IReadOnlyList<ControlPointInterface> _controlPointInterfaces;

    private readonly bool _isClientsProvided;

    private IObservable<HttpRequestResponse>? _httpListenerObservable;

    /// <inheritdoc />
    public bool IsStarted { get; private set; }

    /// <summary>
    /// Creates a control point that listens on the given local IP addresses. Each
    /// address gets a UDP client joined to the SSDP multicast group and a TCP
    /// listener for unicast responses; more than one address creates a multi-homed
    /// control point.
    /// </summary>
    /// <param name="ipAddressParam">One or more local IPv4 addresses to bind.</param>
    /// <exception cref="SSDPException">No address was given, or an address could not be tied to a network interface.</exception>
    public ControlPoint(params IPAddress[] ipAddressParam)
    {
        if (ipAddressParam is null || ipAddressParam.Length == 0)
        {
            throw new SSDPException("At least one IP Address must be specified");
        }

        _controlPointInterfaces = ipAddressParam.Select(CreateInterface).ToList();
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

    private static ControlPointInterface CreateInterface(IPAddress ipAddress)
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
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpClient.Client.Bind(new IPEndPoint(ipAddress, Constants.UdpSSDPMulticastPort));
        udpClient.JoinMulticastGroup(IPAddress.Parse(Constants.UdpSSDPMultiCastAddress), ipAddress);

        return new ControlPointInterface
        {
            IpAddress = ipAddress,
            UdpClient = udpClient,
            TcpListener = new TcpListener(new IPEndPoint(ipAddress, Constants.TcpResponseListenerPort))
            {
                ExclusiveAddressUse = false
            }
        };
    }

    /// <inheritdoc />
    /// <exception cref="SSDPException">An interface has neither a UDP client nor a TCP listener.</exception>
    public void Start(CancellationToken ct)
    {
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

        _httpListenerObservable = listenerObservables
            .Merge()
            .Publish()
            .RefCount();

        IsStarted = true;
    }

    /// <inheritdoc />
    public void HotStart(IObservable<HttpRequestResponse> httpListenerObservable)
    {
        _httpListenerObservable = httpListenerObservable;

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
        Started()
            .Where(x => x.MessageType == MessageType.Response)
            .Select(SsdpMessageParser.ParseMSearchResponse)
            .Where(result => result.IsSuccess)
            .Select(result => result.Value!);

    /// <inheritdoc />
    /// <remarks>
    /// Only <c>ssdp:alive</c>, <c>ssdp:byebye</c> and <c>ssdp:update</c>
    /// notifications are emitted; other or unparsable messages are dropped.
    /// </remarks>
    /// <exception cref="SSDPException">The control point has not been started.</exception>
    public IObservable<Notify> NotifyObservable() =>
        Started()
            .Where(x => x.MessageType == MessageType.Request)
            .Where(req => req.Method == "NOTIFY")
            .Select(SsdpMessageParser.ParseNotify)
            .Where(result => result.IsSuccess)
            .Select(result => result.Value!)
            .Where(notify => notify.NTS is NTS.Alive or NTS.ByeBye or NTS.Update);

    private IObservable<HttpRequestResponse> Started() =>
        _httpListenerObservable ?? throw new SSDPException("Control Point not started.");

    /// <inheritdoc />
    /// <exception cref="SSDPException">
    /// The control point has not been started, <paramref name="ipAddress"/> is not
    /// one of its interfaces, or the request is not fully specified.
    /// </exception>
    public async Task SendMSearchAsync(MSearchRequest mSearch, IPAddress ipAddress)
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
                await cp.UdpClient.SendAsync(
                    dataGram,
                    dataGram.Length,
                    new IPEndPoint(IPAddress.Parse(Constants.UdpSSDPMultiCastAddress), Constants.UdpSSDPMulticastPort));
                break;
            case TransportType.Unicast when mSearch.RemoteIpEndPoint is not null:
                await SendOnTcpAsync(mSearch.RemoteIpEndPoint, dataGram);
                break;
            case TransportType.Unicast:
                throw new SSDPException("A unicast M-SEARCH requires a RemoteIpEndPoint.");
            default:
                throw new SSDPException("M-SEARCH must be either multicast or unicast.");
        }
    }

    private static async Task SendOnTcpAsync(IPEndPoint ipEndPoint, byte[] data)
    {
        using var tcpClient = new TcpClient();

        await tcpClient.ConnectAsync(ipEndPoint.Address, ipEndPoint.Port);

        var stream = tcpClient.GetStream();

        await stream.WriteAsync(data);
        await stream.FlushAsync();
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
