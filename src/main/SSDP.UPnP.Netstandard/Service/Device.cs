using System;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using ISimpleHttpServer.Service;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;
using ISSDP.UPnP.PCL.Interfaces.Service;
using SSDP.UPnP.PCL.Helper;
using SSDP.UPnP.PCL.Model;
using SSDP.UPnP.PCL.Service.Base;
using Convert = SSDP.UPnP.PCL.Helper.Convert;

namespace SSDP.UPnP.PCL.Service
{
    public class Device : CommonBase, IDevice
    {
        private readonly IHttpListener _httpListener;

        public IObservable<IMSearchRequest> MSearchObservable =>
            _httpListener
                .HttpRequestObservable
                .Where(x => !x.IsUnableToParseHttp && !x.IsRequestTimedOut)
                .Where(req => req.Method == "M-SEARCH")
                .Select(req => new MSearchRequest(req));

        public Device(IHttpListener httpListener)
        {
            _httpListener = httpListener;
        }

        public async Task MSearchResponse(IMSearchResponse mSearchResponse, IMSearchRequest mSearchRequest)
        {
            if (mSearchResponse.ResponseCastMethod != CastMethod.Unicast)
            {
                throw new ArgumentException("Cannot only MSearch Response as Unicast");
                //await _httpListener.SendOnMulticast(ComposeMSearchResponseDatagram(mSearchResponse));
            }

            if (int.TryParse(mSearchRequest.TCPPORT, out int tcpSpecifiedRemotePort))
            {
                await SendOnTcp(mSearchResponse.HostIp, tcpSpecifiedRemotePort,
                    ComposeMSearchResponseDatagram(mSearchResponse));
            }
            else
            {
                await SendOnTcp(mSearchResponse.HostIp, mSearchResponse.HostPort,
                    ComposeMSearchResponseDatagram(mSearchResponse));
            }
        }

        public async Task Notify(INotifySsdp notifySsdp)
        {
            // Insert random delay according to UPnP 2.0 spec. section 1.2.1 (page 27).
            var wait = new Random();
            await Task.Delay(TimeSpan.FromMilliseconds(wait.Next(0, 100)));

            // According to the UPnP spec the UDP Multicast Notify should be send three times
            for (var i = 0; i < 3; i++)
            {
                await _httpListener.SendOnMulticast(ComposeNotifyDatagram(notifySsdp));
                // Random delay between resends of 200 - 400 milliseconds. 
                await Task.Delay(TimeSpan.FromMilliseconds(wait.Next(200, 400)));
            }
        }

        private static byte[] ComposeMSearchResponseDatagram(IMSearchResponse response)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append($"HTTP/1.1 {response.StatusCode} {response.ResponseReason}\r\n");
            stringBuilder.Append($"CACHE-CONTROL: max-age = {response.CacheControl.TotalSeconds}\r\n");
            stringBuilder.Append($"DATE: {DateTime.Now:r}\r\n");
            stringBuilder.Append($"EXT:\r\n");
            stringBuilder.Append($"LOCATION: {response.Location}\r\n");
            stringBuilder.Append($"SERVER: " +
                                 $"{response.Server.OperatingSystem}/{response.Server.OperatingSystemVersion}/" +
                                 $" " +
                                 $"UPnP/{response.Server.UpnpMajorVersion}.{response.Server.UpnpMinorVersion}" +
                                 $" " +
                                 $"{response.Server.ProductName}/{response.Server.ProductVersion}\r\n");
            stringBuilder.Append($"ST: {response.ST}\r\n");
            stringBuilder.Append($"USN: {response.USN}\r\n");
            stringBuilder.Append($"BOOTID.UPNP.ORG: {response.BOOTID}\r\n");

            HeaderHelper.AddOptionalHeader(stringBuilder, "CONFIGID.UPNP.ORG", response.CONFIGID);
            HeaderHelper.AddOptionalHeader(stringBuilder, "SEARCHPORT.UPNP.ORG", response.SEARCHPORT);
            HeaderHelper.AddOptionalHeader(stringBuilder, "SECURELOCATION.UPNP.ORG", response.SECURELOCATION);

            // Adding additional vendor specific headers if they exist.
            if (response.Headers?.Any() ?? false)
            {
                foreach (var header in response.Headers)
                {
                    stringBuilder.Append($"{header.Key}: {header.Value}\r\n");
                }
            }

            stringBuilder.Append("\r\n");
            stringBuilder.Append("\r\n");

            return Encoding.UTF8.GetBytes(stringBuilder.ToString());
        }

        private static byte[] ComposeNotifyDatagram(INotifySsdp notifySsdp)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append("NOTIFY * HTTP/1.1\r\n");

            stringBuilder.Append(notifySsdp.NotifyCastMethod == CastMethod.Multicast
                ? "HOST: 239.255.255.250:1900\r\n"
                : $"HOST: {notifySsdp.HostIp}:{notifySsdp.HostPort}\r\n");

            if (notifySsdp.NTS == NTS.Alive)
            {
                stringBuilder.Append($"CACHE-CONTROL: max-age = {notifySsdp.CacheControl.TotalSeconds}\r\n");
            }

            if (notifySsdp.NTS == NTS.Alive || notifySsdp.NTS == NTS.Update)
            {
                stringBuilder.Append($"LOCATION: {notifySsdp.Location.AbsolutePath}\r\n");
            }

            stringBuilder.Append($"NT: max-age = {notifySsdp.NT}\r\n");
            stringBuilder.Append($"NTS: max-age = {notifySsdp.NTS}\r\n");
            stringBuilder.Append($"USN: max-age = {notifySsdp.USN}\r\n");

            if (notifySsdp.NTS == NTS.Alive)
            {
                stringBuilder.Append($"LOCATION: {notifySsdp.Location.AbsolutePath}\r\n");
            }

            stringBuilder.Append($"NT: {notifySsdp.NT}\r\n");
            stringBuilder.Append($"NTS: {Convert.GetNtsString(notifySsdp.NTS)}\r\n");

            if (notifySsdp.NTS == NTS.Alive)
            {
                stringBuilder.Append($"SERVER: " +
                                 $"{notifySsdp.Server.OperatingSystem}/{notifySsdp.Server.OperatingSystemVersion}/" +
                                 $" " +
                                 $"UPnP/{notifySsdp.Server.UpnpMajorVersion}.{notifySsdp.Server.UpnpMinorVersion}" +
                                 $" " +
                                 $"{notifySsdp.Server.ProductName}/{notifySsdp.Server.ProductVersion}\r\n");
            }

            stringBuilder.Append($"USN: {notifySsdp.USN}\r\n");
            stringBuilder.Append($"BOOTID.UPNP.ORG: {notifySsdp.BOOTID}\r\n");
            stringBuilder.Append($"CONFIGID.UPNP.ORG: {notifySsdp.CONFIGID}\r\n");

            if (notifySsdp.NTS == NTS.Alive || notifySsdp.NTS == NTS.Update)
            {
                HeaderHelper.AddOptionalHeader(stringBuilder, "SEARCHPORT.UPNP.ORG", notifySsdp.SEARCHPORT);
                HeaderHelper.AddOptionalHeader(stringBuilder, "SECURELOCATION.UPNP.ORG", notifySsdp.SECURELOCATION);
            }

            // Adding additional vendor specific headers if such are specified
            foreach (var header in notifySsdp.Headers)
            {
                stringBuilder.Append($"{header.Key}: {header.Value}\r\n");
            }
            stringBuilder.Append("\r\n");

            return Encoding.UTF8.GetBytes(stringBuilder.ToString());
        }
    }
}
