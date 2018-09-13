using System;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HttpMachine;
using ISimpleHttpListener.Rx.Model;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;
using ISSDP.UPnP.PCL.Interfaces.Service;
using SimpleHttpListener.Rx.Extension;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Helper;
using SSDP.UPnP.PCL.Model;
using SSDP.UPnP.PCL.Service.Base;
using static SSDP.UPnP.PCL.Helper.Constants;

namespace SSDP.UPnP.PCL.Service
{
    public class ControlPoint : CommonBase, IControlPoint
    {
        private readonly IPEndPoint _ipUdpEndPoint;
        private readonly IPEndPoint _ipTcpResponseEndPoint;
        private readonly IPEndPoint _localEnpoint;

        private bool _isStarted;

        private IObservable<IHttpRequestResponse> _udpMulticastHttpListener;

        private IObservable<IHttpRequestResponse> _tcpMulticastHttpListener;

        private UdpClient _udpClient;


        public ControlPoint(IPAddress ipAddress)
        {
            _localEnpoint = new IPEndPoint(ipAddress, UdpSSDPMulticastPort);

            _ipUdpEndPoint = new IPEndPoint(IPAddress.Parse(UdpSSDPMultiCastAddress), UdpSSDPMulticastPort);

            _ipTcpResponseEndPoint = new IPEndPoint(ipAddress, TcpResponseListenerPort);
        }

        public void Start(CancellationToken ct)
        {
            _udpClient = new UdpClient
            {
                ExclusiveAddressUse = false,
                MulticastLoopback = true
            };

            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            _udpClient.JoinMulticastGroup(IPAddress.Parse(UdpSSDPMultiCastAddress));

            _udpClient.Client.Bind(_localEnpoint);

            _udpMulticastHttpListener = _udpClient.ToHttpListenerObservable(ct).Publish().RefCount();

            var tcpListener = new TcpListener(_ipTcpResponseEndPoint)
            {
                ExclusiveAddressUse = false
            };

            _tcpMulticastHttpListener = tcpListener.ToHttpListenerObservable(ct).Publish().RefCount();

            _isStarted = true;
        }

        public async Task<IObservable<IMSearchResponse>> CreateMSearchResponseObservable()
        {
            if (!_isStarted)
            {
                throw new Exception("Not Control Point started...");
            }

            return _tcpMulticastHttpListener
                .Merge(_udpMulticastHttpListener)
                .Where(x => x.MessageType == MessageType.Response)
                .Select(x => x as IHttpResponse)
                .Where(res => res != null)
                .Select(res => new MSearchResponse(res));
        }

        public async Task<IObservable<INotifySsdp>> CreateNotifyObservable()
        {
            if (!_isStarted)
            {
                throw new Exception("Not Control Point started...");
            }

            return _udpMulticastHttpListener
                .Where(x => x.MessageType == MessageType.Request)
                .Select(x => x as IHttpRequest)
                .Where(x =>
                {
                    return x.ParsingErrors > -1;
                })
                .Where(req => req != null)
                .Where(req => req.Method == "NOTIFY")
                .Select(req => new NotifySsdp(req))
                .Where(n => n.NTS == NTS.Alive || n.NTS == NTS.ByeBye || n.NTS == NTS.Update);
        }
        
        public async Task SendMSearchAsync(IMSearchRequest mSearch)
        {
            if (mSearch.SearchCastMethod == CastMethod.Multicast)
            {
                var datagram = ComposeMSearchDatagram(mSearch);
                await _udpClient.SendAsync(datagram, datagram.Length, _ipUdpEndPoint);
            }

            if (mSearch.SearchCastMethod == CastMethod.Unicast)
            {
                await SendOnTcp(mSearch.Name, mSearch.Port, ComposeMSearchDatagram(mSearch));
            }
        }

        private byte[] ComposeMSearchDatagram(IMSearchRequest request)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append("M-SEARCH * HTTP/1.1\r\n");

            stringBuilder.Append(request.SearchCastMethod == CastMethod.Multicast
                ? "HOST: 239.255.255.250:1900\r\n"
                : $"HOST: {request.Name}:{request.Port}\r\n");

            stringBuilder.Append("MAN: \"ssdp:discover\"\r\n");

            if (request.SearchCastMethod == CastMethod.Multicast)
            {
                stringBuilder.Append($"MX: {request.MX.TotalSeconds}\r\n");
            }
            stringBuilder.Append($"ST: {GetSTSting(request.ST)}\r\n");
            stringBuilder.Append($"USER-AGENT: " +
                                 $"{request.UserAgent.OperatingSystem}/{request.UserAgent.OperatingSystemVersion}" +
                                 $" " +
                                 $"UPnP/{request.UserAgent.UpnpMajorVersion}.{request.UserAgent.UpnpMinorVersion}" +
                                 $" " +
                                 $"{request.UserAgent.ProductName}/{request.UserAgent.ProductVersion}\r\n");

            if (request.SearchCastMethod == CastMethod.Multicast)
            {
                stringBuilder.Append($"CPFN.UPNP.ORG: {request.CPFN}\r\n");

                //stringBuilder.Append($"TCPPORT.UPNP.ORG: {UdpSSDPMulticastPort}\r\n");

                //HeaderHelper.AddOptionalHeader(stringBuilder, "TCPPORT.UPNP.ORG", request.TCPPORT);
                HeaderHelper.AddOptionalHeader(stringBuilder, "CPUUID.UPNP.ORG", request.CPUUID);

                if (request.Headers != null)
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

        private string GetSTSting(IST st)
        {
            switch (st.STtype)
            {
                case STtype.All: return "ssdp:all";
                case STtype.RootDevice: return "upnp:rootdevice";
                case STtype.UIID:
                {
                    return $"uuid:{st.DeviceUUID}";
                }
                case STtype.DeviceType:
                {
                    if (st.HasDomain)
                    {
                        return $"urn:{st.DomainName}:device:{st.Type}:{st.Version}";
                        }
                    else
                    {
                        return $"urn:schemas-upnp-org:device:{st.Type}:{st.Version}";
                        }
                    
                }
                case STtype.ServiceType:
                {
                    if (st.HasDomain)
                    {
                        return $"urn:{st.DomainName}:service:{st.Type}:{st.Version}";
                    }
                    else
                    {
                        return $"urn:schemas-upnp-org:service:{st.Type}:{st.Version}";
                    }
                }
            }

            return null;
        }
    }
}
