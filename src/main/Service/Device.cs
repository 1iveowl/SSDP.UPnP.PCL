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
using static SDPP.UPnP.PCL.Helper.Convert;

namespace SDPP.UPnP.PCL.Service
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

        public async Task MSearchRespone(IMSearchResponse mSearch)
        {
            if (mSearch.ResponseCastMethod == CastMethod.Multicast)
            {
                await _httpListener.SendOnMulticast(ComposeMSearchResponseDatagram(mSearch));
            }

            if (mSearch.ResponseCastMethod == CastMethod.Unicast)
            {
                await SendOnTcp(mSearch.HostIp, mSearch.HostPort, ComposeMSearchResponseDatagram(mSearch));
            }
        }

        public async Task Notify(INotify notify)
        {
            // Insert random delay according to UPnP 2.0 spec. section 1.2.1 (page 27).
            var wait = new Random();
            await Task.Delay(TimeSpan.FromMilliseconds(wait.Next(0, 100)));

            // According to the UPnP spec the UDP Multicast Notify should be send three times
            for (var i = 0; i < 3; i++)
            {
                await _httpListener.SendOnMulticast(ComposeNotifyDatagram(notify), TTL: 2);
                // Random delay between resends of 200 - 400 milliseconds. 
                await Task.Delay(TimeSpan.FromMilliseconds(wait.Next(200, 400)));
            }
        }

        private static byte[] ComposeMSearchResponseDatagram(IMSearchResponse response)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append($"HTTP/1.1 {response.StatusCode} {response.ResponseReason}\r\n");
            stringBuilder.Append($"CACHE-CONTROL: max-age = {response.CacheControl.TotalSeconds}\r\n");
            stringBuilder.Append($"DATE: {DateTime.Now.ToString("r")}\r\n");
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

            AddOptionalHeader(stringBuilder, "CONFIGID.UPNP.ORG", response.CONFIGID);
            AddOptionalHeader(stringBuilder, "SEARCHPORT.UPNP.ORG", response.SEARCHPORT);
            AddOptionalHeader(stringBuilder, "SECURELOCATION.UPNP.ORG", response.SECURELOCATION);

            // Adding additional vendor specific headers if they exist.
            foreach (var header in response.Headers)
            {
                stringBuilder.Append($"{header.Key}: {header.Value}\r\n");
            }
            stringBuilder.Append("\r\n");

            stringBuilder.Append("\r\n");

            return Encoding.UTF8.GetBytes(stringBuilder.ToString());
        }

        private static byte[] ComposeNotifyDatagram(INotify notify)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append("NOTIFY * HTTP/1.1\r\n");

            stringBuilder.Append(notify.NotifyCastMethod == CastMethod.Multicast
                ? "HOST: 239.255.255.250:1900\r\n"
                : $"HOST: {notify.HostIp}:{notify.HostPort}\r\n");

            if (notify.NTS == NTS.Alive)
            {
                stringBuilder.Append($"CACHE-CONTROL: max-age = {notify.CacheControl.TotalSeconds}\r\n");
            }

            if (notify.NTS == NTS.Alive || notify.NTS == NTS.Update)
            {
                stringBuilder.Append($"LOCATION: {notify.Location.AbsolutePath}\r\n");
            }

            stringBuilder.Append($"NT: max-age = {notify.NT}\r\n");
            stringBuilder.Append($"NTS: max-age = {notify.NTS}\r\n");
            stringBuilder.Append($"USN: max-age = {notify.USN}\r\n");

            if (notify.NTS == NTS.Alive)
            {
                stringBuilder.Append($"LOCATION: {notify.Location.AbsolutePath}\r\n");
            }

            stringBuilder.Append($"NT: {notify.NT}\r\n");
            stringBuilder.Append($"NTS: {GetNtsString(notify.NTS)}\r\n");

            if (notify.NTS == NTS.Alive)
            {
                stringBuilder.Append($"SERVER: " +
                                 $"{notify.Server.OperatingSystem}/{notify.Server.OperatingSystemVersion}/" +
                                 $" " +
                                 $"UPnP/{notify.Server.UpnpMajorVersion}.{notify.Server.UpnpMinorVersion}" +
                                 $" " +
                                 $"{notify.Server.ProductName}/{notify.Server.ProductVersion}\r\n");
            }

            stringBuilder.Append($"USN: {notify.USN}\r\n");
            stringBuilder.Append($"BOOTID.UPNP.ORG: {notify.BOOTID}\r\n");
            stringBuilder.Append($"CONFIGID.UPNP.ORG: {notify.CONFIGID}\r\n");

            if (notify.NTS == NTS.Alive || notify.NTS == NTS.Update)
            {
                AddOptionalHeader(stringBuilder, "SEARCHPORT.UPNP.ORG", notify.SEARCHPORT);
                AddOptionalHeader(stringBuilder, "SECURELOCATION.UPNP.ORG", notify.SECURELOCATION);
            }

            // Adding additional vendor specific headers if such are specified
            foreach (var header in notify.Headers)
            {
                stringBuilder.Append($"{header.Key}: {header.Value}\r\n");
            }
            stringBuilder.Append("\r\n");

            return Encoding.UTF8.GetBytes(stringBuilder.ToString());
        }
    }
}
