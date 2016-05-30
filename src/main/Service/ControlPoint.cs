using System;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Interfaces.Model;
using ISDPP.UPnP.PCL.Interfaces.Service;
using ISimpleHttpServer.Service;
using SDPP.UPnP.PCL.Model;

namespace SDPP.UPnP.PCL.Service
{
    public class ControlPoint : IControlPoint
    {
        private readonly IHttpListener _httpListener;

        public IObservable<INotify> NotifyObservable =>
            _httpListener
                .HttpRequestObservable
                .Where(req => req.Method == "NOTIFY")
                .Select(req => new Notify(req));

        public IObservable<IMSearchResponse> MSearchResponseObservable => 
            _httpListener
            .HttpResponseObservable
            .Where(x => !x.IsUnableToParseHttp && !x.IsRequestTimedOut)
            .Select(response => new MSearchResponse(response));

        public ControlPoint(IHttpListener httpListener)
        {
            _httpListener = httpListener;
        }

        public async Task SendMSearch(IMSearch mSearch)
        {
            await _httpListener.SendOnMulticast(ComposeMSearchDatagram(mSearch));
        }

        private byte[] ComposeMSearchDatagram(IMSearch mSearch)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append("M-SEARCH * HTTP/1.1\r\n");

            stringBuilder.Append(mSearch.SearchCastMethod == SearchCastMethod.Multicast
                ? "HOST: 239.255.255.250:1900\r\n"
                : $"HOST: {mSearch.HostIp}:{mSearch.HostPort}\r\n");

            stringBuilder.Append("MAN: \"ssdp:discover\"\r\n");

            if (mSearch.SearchCastMethod == SearchCastMethod.Multicast)
            {
                stringBuilder.Append($"MX: {mSearch.MX}\r\n");
            }
            stringBuilder.Append($"ST: {mSearch.ST}\r\n");
            stringBuilder.Append($"USER-AGENT: {mSearch.UserAgent.OperatingSystem}/" +
                                 $"{mSearch.UserAgent.OperatingSystemVersion}/" +
                                 $" " +
                                 $"UPnP/2.0" +
                                 $" " +
                                 $"{mSearch.UserAgent.ProductName}/" +
                                 $"{mSearch.UserAgent.ProductVersion}\r\n");

            if (mSearch.SearchCastMethod == SearchCastMethod.Multicast)
            {
                stringBuilder.Append($"CPFN.UPNP.ORG: {mSearch.ControlPointFriendlyName}\r\n");
                stringBuilder.Append($"TCPPORT.UPNP.ORG:50000\r\n");
                foreach (var header in mSearch.SdppHeaders)
                {
                    stringBuilder.Append($"{header.Key}: {header.Value}\r\n");
                }
            }

            stringBuilder.Append("\r\n");
            return Encoding.UTF8.GetBytes(stringBuilder.ToString());
        }
    }
}
