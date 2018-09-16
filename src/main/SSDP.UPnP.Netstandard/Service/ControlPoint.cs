using System;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HttpMachine;
using ISimpleHttpListener.Rx.Enum;
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
        private readonly IPEndPoint _localEndpoint;

        private bool _isStarted;

        private IObservable<IHttpRequestResponse> _udpMulticastHttpListener;

        private IObservable<IHttpRequestResponse> _tcpMulticastHttpListener;

        private UdpClient _udpClient;


        public ControlPoint(IPAddress ipAddress)
        {
            _localEndpoint = new IPEndPoint(ipAddress, UdpSSDPMulticastPort);

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

            //_udpClient.Client.ReceiveBufferSize = 8 * 4092;

            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            _udpClient.JoinMulticastGroup(IPAddress.Parse(UdpSSDPMultiCastAddress));

            _udpClient.Client.Bind(_localEndpoint);

            _udpMulticastHttpListener = _udpClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError).Publish().RefCount();

            var tcpListener = new TcpListener(_ipTcpResponseEndPoint)
            {
                ExclusiveAddressUse = false
            };

            _tcpMulticastHttpListener = tcpListener.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError).Publish().RefCount();

            _isStarted = true;
        }

        public IObservable<IMSearchResponse> CreateMSearchResponseObservable()
        {
            if (!_isStarted)
            {
                throw new Exception("No Control Point started...");
            }

            return _tcpMulticastHttpListener
                .Merge(_udpMulticastHttpListener)
                .Where(x => x.MessageType == MessageType.Response)
                .Select(x => x as IHttpResponse)
                .Where(res => res != null)
                .Select(res => new MSearchResponse(res));
        }

        public IObservable<INotifySsdp> CreateNotifyObservable()
        {
            if (!_isStarted)
            {
                throw new Exception("No Control Point started...");
            }

            return _udpMulticastHttpListener
                .Where(x => x.MessageType == MessageType.Request)
                .Select(x => x as IHttpRequest)
                .Where(req => req != null)
                .Where(req => req.Method == "NOTIFY")
                .Select(req => new NotifySsdp(req))
                .Where(n => n.NTS == NTS.Alive || n.NTS == NTS.ByeBye || n.NTS == NTS.Update);
        }
        
        public async Task SendMSearchAsync(IMSearchRequest mSearch)
        {
            var dataGram = ComposeMSearchRequestDataGram(mSearch);

            switch (mSearch?.SearchCastMethod)
            {
                case CastMethod.Multicast:
                    await _udpClient.SendAsync(dataGram, dataGram.Length, _ipUdpEndPoint);
                    break;
                case CastMethod.Unicast:
                    await SendOnTcpASync(mSearch.Name, mSearch.Port, dataGram);
                    break;
                case CastMethod.NoCast:
                    throw new SSDPException("M-SEARCH must be either Multicast or Unicast.");
                case null:
                    throw new SSDPException("M-SEARCH cannot be null.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(mSearch));
            }
        }

        private byte[] ComposeMSearchRequestDataGram(IMSearchRequest request)
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
            switch (st.StSearchType)
            {
                case STSearchType.All: return "ssdp:all";
                case STSearchType.RootDeviceSearch: return "upnp:rootdevice";
                case STSearchType.UIIDSearch:
                {
                    return $"uuid:{st.DeviceUUID}";
                }
                case STSearchType.DeviceTypeSearch:
                {
                    if (string.IsNullOrEmpty(st.DeviceType))
                    {
                        throw new SSDPException("Device Type Search requires a Device Type to be specified.");
                    }

                    if (string.IsNullOrEmpty(st.Version))
                    {
                        throw new SSDPException("Device Type Search requires a version to be specified.");
                    }

                    return $"urn:schemas-upnp-org:device:{st.DeviceType}:{st.Version}";
                    
                }
                case STSearchType.ServiceTypeSearch:
                {
                    if (string.IsNullOrEmpty(st.ServiceType))
                    {
                        throw new SSDPException("Service Type Search requires a Service Type to be specified.");
                    }

                    if (string.IsNullOrEmpty(st.Version))
                    {
                        throw new SSDPException("Service Type Search requires a version to be specified.");
                    }

                    return $"urn:schemas-upnp-org:service:{st.ServiceType}:{st.Version}";
                }
                case STSearchType.DomainDeviceSearch:

                    if (string.IsNullOrEmpty(st.Domain))
                    {
                        throw new SSDPException("Domain Device Type Search requires a Domain Type to be specified.");
                    }

                    if (string.IsNullOrEmpty(st.DeviceType))
                    {
                        throw new SSDPException("Device Type Search requires a Device Type to be specified.");
                    }

                    if (string.IsNullOrEmpty(st.Version))
                    {
                        throw new SSDPException("Device Type Search requires a version to be specified.");
                    }

                    return $"urn:{st.Domain}:device:{st.DeviceType}:{st.Version}";

                case STSearchType.DomainServiceSearch:

                    if (string.IsNullOrEmpty(st.Domain))
                    {
                        throw new SSDPException("Service Service Type Search requires a Domain Type to be specified.");
                    }
                    
                    if (string.IsNullOrEmpty(st.ServiceType))
                    {
                        throw new SSDPException("Service Type Search requires a Service Type to be specified.");
                    }

                    if (string.IsNullOrEmpty(st.Version))
                    {
                        throw new SSDPException("Device Type Search requires a version to be specified.");
                    }

                    return $"urn:{st.Domain}:service:{st.ServiceType}:{st.Version}";

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
