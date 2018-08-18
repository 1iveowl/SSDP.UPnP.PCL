using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ISimpleHttpServer.Service;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;
using ISSDP.UPnP.PCL.Interfaces.Service;
using SSDP.UPnP.Netstandard.Helper;
using SSDP.UPnP.PCL.Helper;
using SSDP.UPnP.PCL.Model;
using SSDP.UPnP.PCL.Service.Base;

namespace SSDP.UPnP.PCL.Service
{
    public class ControlPoint : CommonBase, IControlPoint
    {
        private readonly IHttpListener _httpListener;

        public async Task<IObservable<IMSearchResponse>> CreateMSearchResponseObservable(int tcpReponsePort)
        {
            var multicastResObs = await _httpListener.UdpMulticastHttpResponseObservable(
                Initializer.UdpSSDPMultiCastAddress,
                Initializer.UdpSSDPMulticastPort, 
                false);

            var tcpResObs = await _httpListener.TcpHttpResponseObservable(tcpReponsePort, false);

            return multicastResObs
                .Merge(tcpResObs)
                .Where(x => !x.IsUnableToParseHttp && !x.IsRequestTimedOut)
                .Select(res => new MSearchResponse(res));
        }

        public async Task<IObservable<INotifySsdp>> CreateNotifyObservable(bool allowMultipleBindingToPort = false)
        {
            var multicastReqObs = await _httpListener.UdpMulticastHttpRequestObservable(
                    Initializer.UdpSSDPMultiCastAddress,
                    Initializer.UdpSSDPMulticastPort, 
                    allowMultipleBindingToPort);

            return multicastReqObs
                .Where(x => !x.IsUnableToParseHttp && !x.IsRequestTimedOut)
                .Where(req => req.Method == "NOTIFY")
                .Select(req => new NotifySsdp(req))
                .Where(n => n.NTS == NTS.Alive || n.NTS == NTS.ByeBye || n.NTS == NTS.Update);
        }
        
        public ControlPoint(IHttpListener httpListener)
        {
            _httpListener = httpListener;
        }

        public async Task SendMSearchAsync(IMSearchRequest mSearch)
        {
            if (mSearch.SearchCastMethod == CastMethod.Multicast)
            {
                await _httpListener.SendOnMulticast(ComposeMSearchDatagram(mSearch));
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

                HeaderHelper.AddOptionalHeader(stringBuilder, "TCPPORT.UPNP.ORG", request.TCPPORT);
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
