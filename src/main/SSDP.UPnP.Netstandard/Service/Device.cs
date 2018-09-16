using System;
using System.Collections.Generic;
using System.Linq;
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
using NLog;
using SimpleHttpListener.Rx.Extension;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Helper;
using SSDP.UPnP.PCL.Model;
using SSDP.UPnP.PCL.Service.Base;
using Convert = SSDP.UPnP.PCL.Helper.Convert;
using static SSDP.UPnP.PCL.Helper.Constants;

namespace SSDP.UPnP.PCL.Service
{
    public class Device : CommonBase, IDevice
    {
        //private readonly IPEndPoint _multicastEndPoint;
        //private readonly IPEndPoint _deviceMulticastEndpoint;
        //private readonly IPEndPoint _deviceUnicastEndpoint;

        //private UdpClient _multicastClient;
        //private UdpClient _unicastClient;

        private readonly IEnumerable<IRootDeviceInterface> _rootDeviceInterfaces;

        private IObservable<IHttpRequestResponse> _udpMulticastHttpListener;

        private IObservable<IHttpRequestResponse> _udpUnicastHttpListener;

        //private IObservable<IHttpRequestResponse> _tcpMulticastHttpListener;

        public ILogger Logger { get; set; }

        //public Uri Location { get; set; }
        //public IServer Server { get; set; }
        //public IEnumerable<IUSN> USNs { get; set; }
        //public IST ST { get; set; }
        //public int SEARCHPORT { get; private set; }


        public bool IsStarted { get; private set; }

        public bool IsMultiHomed => _rootDeviceInterfaces.Count() > 1;

        public Device(IRootDevice rootDevice)
        {
            if (rootDevice?.IpEndPoint == null)
            {
                throw new SSDPException("At least one Root Device must be fully specified.");
            }
            var rootDeviceInterface = new RootDeviceInterface
            {
                RootDevice = rootDevice,
                UdpMulticastClient = new UdpClient
                {
                    ExclusiveAddressUse = false,
                    MulticastLoopback = true
                },
                UdpUnicastClient = new UdpClient
                {
                    ExclusiveAddressUse = false
                }
            };

            rootDeviceInterface.UdpMulticastClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            rootDeviceInterface.UdpMulticastClient.JoinMulticastGroup(IPAddress.Parse(UdpSSDPMultiCastAddress));

            rootDeviceInterface.UdpMulticastClient.Client.Bind(new IPEndPoint(rootDevice.IpEndPoint?.Address, UdpSSDPMulticastPort));


            rootDeviceInterface.UdpUnicastClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            rootDeviceInterface.UdpUnicastClient.Client.Bind(new IPEndPoint(rootDevice.IpEndPoint.Address, rootDevice.IpEndPoint.Port));

            _rootDeviceInterfaces = new List<IRootDeviceInterface> {rootDeviceInterface};

        }

        public Device(params IRootDeviceInterface[] rootDeviceInterfaces)
        {
            _rootDeviceInterfaces = rootDeviceInterfaces;

            //SEARCHPORT = ipEndPoint.Port;

            //_deviceMulticastEndpoint = new IPEndPoint(ipEndPoint.Address, UdpSSDPMulticastPort);

            //_deviceUnicastEndpoint = new IPEndPoint(ipEndPoint.Address, SEARCHPORT);

            //_multicastEndPoint = new IPEndPoint(IPAddress.Parse(UdpSSDPMultiCastAddress), UdpSSDPMulticastPort);
        }


        public void Start(CancellationToken ct)
        {

            if (!_rootDeviceInterfaces?.Any() ?? false)
            {
                throw new SSDPException("No Root Device specified.");
            }

            foreach (var rootDevice in _rootDeviceInterfaces)
            {
                if (_udpMulticastHttpListener == null)
                {
                    _udpMulticastHttpListener = rootDevice.UdpMulticastClient
                        .ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError)
                        .Publish().RefCount();
                }
                else
                {
                    _udpMulticastHttpListener = _udpMulticastHttpListener.Merge(
                        rootDevice.UdpMulticastClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError))
                        .Publish().RefCount();
                }

                if (_udpUnicastHttpListener == null)
                {
                    _udpUnicastHttpListener = rootDevice.UdpUnicastClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError)
                        .Publish().RefCount();
                }
                else
                {
                    _udpUnicastHttpListener = _udpUnicastHttpListener.Merge(
                        rootDevice.UdpUnicastClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError))
                        .Publish().RefCount();
                }
            }

            IsStarted = true;

            //var multicastClient = new UdpClient
            //{
            //    ExclusiveAddressUse = false,
            //    MulticastLoopback = true
            //};

            ////_udpClient.Client.ReceiveBufferSize = 8 * 4092;

            //_multicastClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            //_multicastClient.JoinMulticastGroup(IPAddress.Parse(UdpSSDPMultiCastAddress));

            //_multicastClient.Client.Bind(_deviceMulticastEndpoint);


            //var unicastClient = new UdpClient
            //{
            //    ExclusiveAddressUse = false
            //};

            //_unicastClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            //_unicastClient.Client.Bind(_deviceUnicastEndpoint);

            //Start(multicastClient, unicastClient, ct);
        }
        
        //private void Start(UdpClient multicastClint, UdpClient unicastClient, CancellationToken ct)
        //{
        //    _multicastClient = multicastClint;
        //    _unicastClient = unicastClient;

        //    _udpMulticastHttpListener =
        //        multicastClint.ToHttpListenerObservable(ct,
        //            ErrorCorrection.HeaderCompletionError); //.Publish().RefCount();

        //    _udpUnicastHttpListener = unicastClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError);

        //    IsStarted = true;
        //}

        public void Stop()
        {
            throw new NotImplementedException();
        }

        // Constructor overload provides the opportunity to define the UDP Multicast and UDP Unicast client elsewhere.
        // This could come in handy if combining this SSDP library with UPnP Eventing and sharing the UDP clients between the UPnP layers.


        private IObservable<IMSearchResponse> MSearchRequestObservable()
        {
            //if (!_multicastClient?.Client?.IsBound ?? false)
            //{
            //    _multicastClient.Client.Bind(_multicastEndPoint);
            //}

            return _udpMulticastHttpListener
                .Where(x => x.MessageType == MessageType.Request)
                .Select(x => x as IHttpRequest)
                .Where(req => req != null)
                .Where(req => req?.Method == "M-SEARCH")
                .Select(req => new MSearchRequest(req))
                .Do(LogRequest)
                .SelectMany(mSearchReq =>
                {
                    var responseList = new List<IMSearchResponse>();

                    switch (mSearchReq.ST.StSearchType)
                    {
                        case STSearchType.All:
                            responseList.Add(new MSearchResponse());
                            break;
                        case STSearchType.RootDeviceSearch:
                            break;
                        case STSearchType.UIIDSearch:

                            break;
                        case STSearchType.DeviceTypeSearch:
                            break;
                        case STSearchType.ServiceTypeSearch:
                            break;
                        case STSearchType.DomainDeviceSearch:
                            break;
                        case STSearchType.DomainServiceSearch:
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    return responseList;
                })
                .Where(res => res != null)
                .Select(res => res);
        }

        //public async Task SendMSearchResponseAsync(IMSearchResponse mSearchResponse, IPAddress ipAddress)
        //{
        //    var wait = new Random();

        //    await Task.Delay(TimeSpan.FromMilliseconds(wait.Next(50, (int) mSearchResponse.MX.TotalMilliseconds)));

        //    if (mSearchResponse.ResponseCastMethod != CastMethod.Unicast)
        //    {
        //        var datagram = ComposeMSearchResponseDatagram(mSearchResponse);
        //        await _multicastClient.SendAsync(datagram, datagram.Length, _multicastEndPoint);
        //    }

        //    await SendOnTcpASync(mSearchResponse.RemoteHost.Name, mSearchResponse.RemoteHost.Port,
        //        ComposeMSearchResponseDatagram(mSearchResponse));

        //    //if (int.TryParse(mSearchResponse.RemoteHost.Port, out var tcpSpecifiedRemotePort))
        //    //{
        //    //    await SendOnTcp(mSearchResponse.RemoteHost.Name, tcpSpecifiedRemotePort,
        //    //        ComposeMSearchResponseDatagram(mSearchResponse));
        //    //}
        //    //else
        //    //{
        //    //    await SendOnTcp(mSearchResponse.RemoteHost.Name, mSearchResponse.RemoteHost.Port,
        //    //        ComposeMSearchResponseDatagram(mSearchResponse));
        //    //}
        //}

        public async Task SendNotifyAsync(INotifySsdp notifySsdp)
        {
            // Insert random delay according to UPnP 2.0 spec. section 1.2.1 (page 27).
            var wait = new Random();
            await Task.Delay(TimeSpan.FromMilliseconds(wait.Next(50, 100)));

            // According to the UPnP spec the UDP Multicast Notify should be send three times
            for (var i = 0; i < 3; i++)
            {
                var datagram = ComposeNotifyDatagram(notifySsdp);
                await _multicastClient.SendAsync(datagram, datagram.Length, _multicastEndPoint);
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

        private void LogRequest(IMSearchRequest req)
        {
            Logger?.Info("---### Device Received a M-SEARCH REQUEST ###---");
            Logger?.Info($"USER-AGENT: " +
                        $"{req.UserAgent?.OperatingSystem}/{req.UserAgent?.OperatingSystemVersion} " +
                        $"UPNP/" +
                        $"{req.UserAgent?.UpnpMajorVersion}.{req.UserAgent?.UpnpMinorVersion}" +
                        $" " +
                        $"{req.UserAgent?.ProductName}/{req.UserAgent?.ProductVersion}" +
                        $" - ({req.UserAgent?.FullString})");
            Logger?.Info($"CPFN: {req.CPFN}");
            Logger?.Info($"CPUUID: {req.CPUUID}");
            Logger?.Info($"TCPPORT: {req.TCPPORT}");

            if (req.Headers.Any())
            {
                Logger?.Info($"Additional Headers: {req.Headers.Count}");
                foreach (var header in req.Headers)
                {
                    Logger?.Info($"{header.Key}: {header.Value}; ");
                }
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}