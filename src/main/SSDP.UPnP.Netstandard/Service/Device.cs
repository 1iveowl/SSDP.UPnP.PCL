using System;
using System.Linq;
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
using SSDP.UPnP.PCL.Helper;
using SSDP.UPnP.PCL.Model;
using SSDP.UPnP.PCL.Service.Base;
using Convert = SSDP.UPnP.PCL.Helper.Convert;
using static SSDP.UPnP.PCL.Helper.Constants;

namespace SSDP.UPnP.PCL.Service
{
    public class Device : CommonBase, IDevice
    {

        private readonly IPEndPoint _ipUdpEndPoint;
        private readonly IPEndPoint _ipTcpRequestEndPoint;

        private readonly UdpClient _udpClient;

        public Device(IPAddress ipAddress)
        {
            _ipUdpEndPoint = new IPEndPoint(ipAddress, UdpSSDPMulticastPort);

            _ipTcpRequestEndPoint = new IPEndPoint(ipAddress, TcpResponseListenerPort);

            _udpClient = new UdpClient
            {
                ExclusiveAddressUse = false
            };

            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            _udpClient.JoinMulticastGroup(IPAddress.Parse(UdpSSDPMultiCastAddress));
        }

        public async Task<IObservable<IMSearchRequest>> CreateMSearchObservable(CancellationToken ct)
        {
            if (!_udpClient?.Client?.IsBound ?? false)
            {
                _udpClient.Client.Bind(_ipUdpEndPoint);
            }

            return _udpClient.ToHttpListenerObservable(ct)
                    .Where(x => x.MessageType == MessageType.Request)
                    .Select(x => x as IHttpRequest)
                    .Where(res => res != null)
                    .Where(req => req?.Method == "M-SEARCH")
                    .Select(req => new MSearchRequest(req));

            //var multicastReqObs = await _httpListener.UdpMulticastHttpRequestObservable(
            //    Initializer.UdpSSDPMultiCastAddress,
            //    Initializer.UdpSSDPMulticastPort,
            //    allowMultipleBindingToPort);

            //return multicastReqObs
            //    .Where(x => !x.IsUnableToParseHttp && !x.IsRequestTimedOut)
            //    .Where(req => req.Method == "M-SEARCH")
            //    .Select(req => new MSearchRequest(req));
        }

        public async Task SendMSearchResponseAsync(IMSearchResponse mSearchResponse, IMSearchRequest mSearchRequest)
        {
            var wait = new Random();
            await Task.Delay(TimeSpan.FromMilliseconds(wait.Next(50, (int)mSearchRequest.MX.TotalMilliseconds)));

            if (mSearchResponse.ResponseCastMethod != CastMethod.Unicast)
            {
                var datagram = ComposeMSearchResponseDatagram(mSearchResponse);
                await _udpClient.SendAsync(datagram, datagram.Length, _ipUdpEndPoint);
            }

            if (int.TryParse(mSearchRequest.TCPPORT, out int tcpSpecifiedRemotePort))
            {
                await SendOnTcp(mSearchRequest.Name, tcpSpecifiedRemotePort,
                    ComposeMSearchResponseDatagram(mSearchResponse));
            }
            else
            {
                await SendOnTcp(mSearchRequest.Name, mSearchRequest.Port,
                    ComposeMSearchResponseDatagram(mSearchResponse));
            }
        }

        public async Task SendNotifyAsync(INotifySsdp notifySsdp)
        {
            // Insert random delay according to UPnP 2.0 spec. section 1.2.1 (page 27).
            var wait = new Random();
            await Task.Delay(TimeSpan.FromMilliseconds(wait.Next(50, 100)));

            // According to the UPnP spec the UDP Multicast Notify should be send three times
            for (var i = 0; i < 3; i++)
            {
                var datagram = ComposeNotifyDatagram(notifySsdp);
                await _udpClient.SendAsync(datagram, datagram.Length, _ipUdpEndPoint);
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
                : $"HOST: {notifySsdp.Name}:{notifySsdp.Port}\r\n");

            if (notifySsdp.NTS == NTS.Alive)
            {
                stringBuilder.Append($"CACHE-CONTROL: max-age = {notifySsdp.CacheControl.TotalSeconds}\r\n");
            }

            if (notifySsdp.NTS == NTS.Alive || notifySsdp.NTS == NTS.Update)
            {
                stringBuilder.Append($"LOCATION: {notifySsdp.Location.AbsoluteUri}\r\n");
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
            if (notifySsdp.Headers?.Any() ?? false)
            {
                foreach (var header in notifySsdp.Headers)
                {
                    stringBuilder.Append($"{header.Key}: {header.Value}\r\n");
                }
            }

            stringBuilder.Append("\r\n");

            return Encoding.UTF8.GetBytes(stringBuilder.ToString());
        }
    }
}
