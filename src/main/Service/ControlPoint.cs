using System;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Enum;
using ISDPP.UPnP.PCL.Interfaces.Model;
using ISDPP.UPnP.PCL.Interfaces.Service;
using ISimpleHttpServer.Service;
using SDPP.UPnP.PCL.Model;
using SDPP.UPnP.PCL.Service.Base;
using static SDPP.UPnP.PCL.Helper.HeaderHelper;

namespace SDPP.UPnP.PCL.Service
{
    public class ControlPoint : CommonBase, IControlPoint
    {
        private readonly IHttpListener _httpListener;

        public IObservable<INotify> NotifyObservable =>
            _httpListener
            .HttpRequestObservable
            .Where(x => !x.IsUnableToParseHttp && !x.IsRequestTimedOut)
            .Where(req => req.Method == "NOTIFY")
            .Select(req => new Notify(req));
            //.Where(n => n.NTS == NTS.Alive || n.NTS == NTS.ByeBye || n.NTS == NTS.Update);

        public IObservable<IMSearchResponse> MSearchResponseObservable =>
            _httpListener
            .HttpResponseObservable
            .Where(x => !x.IsUnableToParseHttp && !x.IsRequestTimedOut)
            .Select(response => new MSearchResponse(response));

        public ControlPoint(IHttpListener httpListener)
        {
            _httpListener = httpListener;
        }

        public async Task SendMSearch(IMSearchRequest mSearch)
        {
            if (mSearch.SearchCastMethod == CastMethod.Multicast)
            {
                await _httpListener.SendOnMulticast(ComposeMSearchDatagram(mSearch));
            }

            if (mSearch.SearchCastMethod == CastMethod.Unicast)
            {
                await SendOnTcp(mSearch.HostIp, mSearch.HostPort, ComposeMSearchDatagram(mSearch));
            }
        }

        private static byte[] ComposeMSearchDatagram(IMSearchRequest request)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append("M-SEARCH * HTTP/1.1\r\n");

            stringBuilder.Append(request.SearchCastMethod == CastMethod.Multicast
                ? "HOST: 239.255.255.250:1900\r\n"
                : $"HOST: {request.HostIp}:{request.HostPort}\r\n");

            stringBuilder.Append("MAN: \"ssdp:discover\"\r\n");

            if (request.SearchCastMethod == CastMethod.Multicast)
            {
                stringBuilder.Append($"MX: {request.MX.TotalSeconds}\r\n");
            }
            stringBuilder.Append($"ST: {request.ST}\r\n");
            stringBuilder.Append($"USER-AGENT: " +
                                 $"{request.UserAgent.OperatingSystem}/{request.UserAgent.OperatingSystemVersion}" +
                                 $" " +
                                 $"UPnP/{request.UserAgent.UpnpMajorVersion}.{request.UserAgent.UpnpMinorVersion}" +
                                 $" " +
                                 $"{request.UserAgent.ProductName}/{request.UserAgent.ProductVersion}\r\n");

            if (request.SearchCastMethod == CastMethod.Multicast)
            {
                stringBuilder.Append($"CPFN.UPNP.ORG: {request.CPFN}\r\n");

                AddOptionalHeader(stringBuilder, "TCPPORT.UPNP.ORG", request.TCPPORT);
                AddOptionalHeader(stringBuilder, "CPUUID.UPNP.ORG", request.CPUUID);

                foreach (var header in request.Headers)
                {
                    stringBuilder.Append($"{header.Key}: {header.Value}\r\n");
                }
            }

            stringBuilder.Append("\r\n");
            return Encoding.UTF8.GetBytes(stringBuilder.ToString());
        }
    }
}
