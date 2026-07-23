using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimpleHttpListener.Rx;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Enum;
using SSDP.UPnP.PCL.Helper;
using SSDP.UPnP.PCL.Interfaces.Model;
using SSDP.UPnP.PCL.Interfaces.Service;
using SSDP.UPnP.PCL.Model;
using static SSDP.UPnP.PCL.Helper.Constants;

namespace SSDP.UPnP.PCL.Service
{
    public class ControlPoint : IControlPoint
    {
        private readonly IEnumerable<IControlPointInterface> _controlPointInterfaces;

        private IObservable<HttpRequestResponse> _httpListenerObservable;

        private readonly bool _isClientsProvided;

        public bool IsStarted { get; private set; }

        public ControlPoint(params IPAddress[] ipAddressParam)
        {
            if (ipAddressParam is null || ipAddressParam.Length == 0)
            {
                throw new SSDPException("At least one IP Address must be specified");
            }

            var controlPointInterfaceList = new List<ControlPointInterface>();

            foreach (var ipAddress in ipAddressParam)
            {
                var cpInterface = new ControlPointInterface
                {
                    IpAddress = ipAddress,
                };

                var udpClient = new UdpClient();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    udpClient.ExclusiveAddressUse = false;
                    udpClient.MulticastLoopback = true;
                }

                var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(nic =>
                        nic.GetIPProperties().UnicastAddresses.FirstOrDefault(addr => Equals(addr.Address, ipAddress)) is not null);

                if (networkInterface is null)
                {
                    throw new SSDPException("Unable to tie IPAddress to network interface.");
                }

                var optionValue = IPAddress.NetworkToHostOrder(networkInterface.GetIPProperties().GetIPv4Properties().Index);

                udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, optionValue);

                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                udpClient.Client.Bind(new IPEndPoint(ipAddress, UdpSSDPMulticastPort));

                udpClient.JoinMulticastGroup(IPAddress.Parse(UdpSSDPMultiCastAddress), ipAddress);

                cpInterface.UdpClient = udpClient;

                cpInterface.TcpListener = new TcpListener(new IPEndPoint(ipAddress, TcpResponseListenerPort))
                {
                    ExclusiveAddressUse = false
                };

                controlPointInterfaceList.Add(cpInterface);
            }

            _controlPointInterfaces = controlPointInterfaceList;
        }

        public ControlPoint(params IControlPointInterface[] controlPointInterfaceParams)
        {
            if (controlPointInterfaceParams is null || controlPointInterfaceParams.Length == 0)
            {
                throw new SSDPException("At least one Control Point Interface must be specified.");
            }

            _controlPointInterfaces = controlPointInterfaceParams;

            _isClientsProvided = true;
        }

        public void Start(CancellationToken ct)
        {
            if (_controlPointInterfaces is null || !_controlPointInterfaces.Any())
            {
                throw new SSDPException("No Control Point interface specified.");
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

            _httpListenerObservable = listenerObservables
                .Merge()
                .Publish()
                .RefCount();

            IsStarted = true;
        }

        public void HotStart(IObservable<HttpRequestResponse> httpListenerObservable)
        {
            _httpListenerObservable = httpListenerObservable;

            IsStarted = true;
        }

        public IObservable<IMSearchResponse> MSearchResponseObservable()
        {
            if (!IsStarted)
            {
                throw new SSDPException("Control Point not started.");
            }

            return _httpListenerObservable
                .Where(x => x.MessageType == MessageType.Response)
                .Select(res => new MSearchResponse(res))
                .Where(res => !res.InvalidRequest);
        }

        public IObservable<INotify> NotifyObservable()
        {
            if (!IsStarted)
            {
                throw new SSDPException("Control Point not started.");
            }

            return _httpListenerObservable
                .Where(x => x.MessageType == MessageType.Request)
                .Where(req => req.Method == "NOTIFY")
                .Select(req => new Notify(req))
                .Where(n => n.NTS == NTS.Alive || n.NTS == NTS.ByeBye || n.NTS == NTS.Update);
        }

        public async Task SendMSearchAsync(IMSearchRequest mSearch, IPAddress ipAddress)
        {
            if (!IsStarted)
            {
                throw new SSDPException("Control Point not started.");
            }

            var cp = _controlPointInterfaces?.FirstOrDefault(c => Equals(c?.IpAddress, ipAddress));

            if (cp?.UdpClient is null)
            {
                throw new SSDPException("IP Address provided is not associated with any ControlPoint EndPoint or no Control Points specified.");
            }

            var dataGram = ComposeMSearchRequestDataGram(mSearch);

            switch (mSearch?.TransportType)
            {
                case TransportType.Multicast:
                    await cp.UdpClient.SendAsync(
                        dataGram,
                        dataGram.Length,
                        new IPEndPoint(IPAddress.Parse(UdpSSDPMultiCastAddress), UdpSSDPMulticastPort));
                    break;
                case TransportType.Unicast:
                    await SendOnTcpASync(mSearch.RemoteIpEndPoint, dataGram);
                    break;
                case TransportType.NoCast:
                    throw new SSDPException("M-SEARCH must be either multicast or unicast.");
                case null:
                    throw new SSDPException("M-SEARCH cannot be null.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(mSearch));
            }
        }

        internal static byte[] ComposeMSearchRequestDataGram(IMSearchRequest request)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append("M-SEARCH * HTTP/1.1\r\n");

            stringBuilder.Append(request.TransportType == TransportType.Multicast
                ? $"HOST: {UdpSSDPMultiCastAddress}:{UdpSSDPMulticastPort}\r\n"
                : $"HOST: {request.HOST}\r\n");

            stringBuilder.Append("MAN: \"ssdp:discover\"\r\n");

            if (request.TransportType == TransportType.Multicast)
            {
                stringBuilder.Append($"MX: {(int)request.MX.TotalSeconds}\r\n");
            }

            stringBuilder.Append($"ST: {GetSTString(request.ST)}\r\n");
            stringBuilder.Append($"USER-AGENT: " +
                                 $"{request.UserAgent.OperatingSystem}/{request.UserAgent.OperatingSystemVersion}" +
                                 $" " +
                                 $"UPnP/{request.UserAgent.UpnpMajorVersion}.{request.UserAgent.UpnpMinorVersion}" +
                                 $" " +
                                 $"{request.UserAgent.ProductName}/{request.UserAgent.ProductVersion}\r\n");

            if (request.TransportType == TransportType.Multicast)
            {
                stringBuilder.Append($"CPFN.UPNP.ORG: {request.CPFN}\r\n");

                HeaderHelper.AddOptionalHeader(stringBuilder, "CPUUID.UPNP.ORG", request.CPUUID);

                if (request.Headers is not null)
                {
                    foreach (var header in request.Headers)
                    {
                        stringBuilder.Append($"{header.Key}: {header.Value}\r\n");
                    }
                }
            }

            stringBuilder.Append("\r\n");
            return Encoding.UTF8.GetBytes(stringBuilder.ToString());
        }

        internal static string GetSTString(IST st)
        {
            switch (st.StSearchType)
            {
                case STType.All:
                    return "ssdp:all";
                case STType.RootDeviceSearch:
                    return "upnp:rootdevice";
                case STType.UIIDSearch:
                    return $"uuid:{st.DeviceUUID}";
                case STType.DeviceTypeSearch:
                {
                    if (string.IsNullOrEmpty(st.TypeName))
                    {
                        throw new SSDPException("Device Type Search requires a Device Type to be specified.");
                    }

                    if (st.Version < 1)
                    {
                        throw new SSDPException("Device Type Search requires a version (1 or greater) to be specified.");
                    }

                    return $"urn:schemas-upnp-org:device:{st.TypeName}:{st.Version}";
                }
                case STType.ServiceTypeSearch:
                {
                    if (string.IsNullOrEmpty(st.TypeName))
                    {
                        throw new SSDPException("Service Type Search requires a Service Type to be specified.");
                    }

                    if (st.Version < 1)
                    {
                        throw new SSDPException("Service Type Search requires a version (1 or greater) to be specified.");
                    }

                    return $"urn:schemas-upnp-org:service:{st.TypeName}:{st.Version}";
                }
                case STType.DomainDeviceSearch:

                    if (string.IsNullOrEmpty(st.Domain))
                    {
                        throw new SSDPException("Domain Device Search requires a Domain to be specified.");
                    }

                    if (string.IsNullOrEmpty(st.TypeName))
                    {
                        throw new SSDPException("Domain Device Search requires a Device Type to be specified.");
                    }

                    if (st.Version < 1)
                    {
                        throw new SSDPException("Domain Device Search requires a version (1 or greater) to be specified.");
                    }

                    return $"urn:{st.Domain}:device:{st.TypeName}:{st.Version}";

                case STType.DomainServiceSearch:

                    if (string.IsNullOrEmpty(st.Domain))
                    {
                        throw new SSDPException("Domain Service Search requires a Domain to be specified.");
                    }

                    if (string.IsNullOrEmpty(st.TypeName))
                    {
                        throw new SSDPException("Domain Service Search requires a Service Type to be specified.");
                    }

                    if (st.Version < 1)
                    {
                        throw new SSDPException("Domain Service Search requires a version (1 or greater) to be specified.");
                    }

                    return $"urn:{st.Domain}:service:{st.TypeName}:{st.Version}";

                default:
                    throw new ArgumentOutOfRangeException(nameof(st));
            }
        }

        private static async Task SendOnTcpASync(IPEndPoint ipEndPoint, byte[] data)
        {
            using var tcpClient = new TcpClient();

            await tcpClient.ConnectAsync(ipEndPoint.Address, ipEndPoint.Port);

            var stream = tcpClient.GetStream();

            await stream.WriteAsync(data, 0, data.Length);
            await stream.FlushAsync();
        }

        public void Dispose()
        {
            if (_isClientsProvided)
            {
                return;
            }

            foreach (var client in _controlPointInterfaces)
            {
                client?.UdpClient?.Dispose();
                client?.TcpListener?.Dispose();
            }
        }
    }
}
