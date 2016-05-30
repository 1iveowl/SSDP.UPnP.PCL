using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ISDPP.UPnP.PCL.Interfaces.Model;
using ISDPP.UPnP.PCL.Interfaces.Service;
using ISimpleHttpServer.Model;
using SDPP.UPnP.PCL.Model;
using SimpleHttpServer.Service;
using SocketLite.Model;

namespace SDPP.UPnP.PCL.Service
{
    public class ControlPointHandler : IControlPointHandler
    {
        private readonly HttpListener _httpListener;

        public IObservable<INotify> NotifyObservable =>
            _httpListener
                .HttpRequestObservable
                .Where(req => req.Method == "NOTIFY")
                .Select(req => new Notify(req));

        public IObservable<IMSearchResponse> MSearchResponseObservable => 
            _httpListener
            .HttpResponseObservable
            .Where(x => !x.IsUnableToParseHttp && !x.IsRequestTimedOut)
            .Select(response => new MSearchResponse
            {
                StatusCode = response.StatusCode,
                ResponseReason = response.ResponseReason,
                CacheControl = GetMaxAge(response.Headers["CACHE-CONTROL"]),
                Server = response.Headers["SERVER"]
            });

        private int GetMaxAge(string str)
        {
            var stringArray = str.Trim().Split('=');
            var maxAgeStr = stringArray[1];

            var maxAge = 0;
            if (maxAgeStr != null)
            {
                int.TryParse(maxAgeStr, out maxAge);
            }
            return maxAge;
        }

        public ControlPointHandler()
        {
            _httpListener = new HttpListener(TimeSpan.FromSeconds(30));
        }

        public async Task Start()
        {
            var comm = new CommunicationsInterface();
            var allComms = comm.GetAllInterfaces();
            var networkComm = allComms.FirstOrDefault(x => x.GatewayAddress != null);

            await _httpListener.StartTcpRequestListener(1900, networkComm);
            await _httpListener.StartTcpResponseListener(1901, networkComm);
            await _httpListener.StartUdpMulticastListener("239.255.255.250", 1900, networkComm);
            await _httpListener.StartUdpListener(1900, networkComm);
        }

        public void Stop()
        {
            _httpListener.StopUdpMultiCastListener();
        }

        public async Task SendMulticast(IMSearch mSearch)
        {
            await _httpListener.SendOnMulticast(ComposeMSearchDatagram(mSearch));
        }

        private string GetHostIp(string host)
        {
            var stringArray = Regex.Split(host, ":");
            return stringArray[0];
        }

        private string GetHostPort(string host)
        {
            var stringArray = Regex.Split(host, ":");
            return stringArray[1];
        }

        private byte[] ComposeMSearchDatagram(IMSearch mSearch)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append("M-SEARCH * HTTP/1.1\r\n");
            stringBuilder.Append(mSearch.IsMulticast
                ? "HOST: 239.255.255.250:1900\r\n"
                : $"HOST: {mSearch.HostIp}:{mSearch.HostPort}\r\n");
            stringBuilder.Append("MAN: \"ssdp:discover\"\r\n");

            if (mSearch.IsMulticast)
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

            if (mSearch.IsMulticast)
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
