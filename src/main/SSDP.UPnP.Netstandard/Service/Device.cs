using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ISimpleHttpListener.Rx.Enum;
using ISimpleHttpListener.Rx.Model;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Model;
using ISSDP.UPnP.PCL.Interfaces.Service;
using NLog;
using SimpleHttpListener.Rx.Extension;
using SSDP.UPnP.PCL.ExtensionMethod;
using SSDP.UPnP.PCL.Handler;
using SSDP.UPnP.PCL.Helper;
using SSDP.UPnP.PCL.Model;
using SSDP.UPnP.PCL.Service.Base;
using static SSDP.UPnP.PCL.Helper.Constants;


namespace SSDP.UPnP.PCL.Service
{
    public class Device : EntityBase, IDevice
    {
        private readonly IObserver<DeviceActivity> _observerDeviceActivity;

        private IDisposable _disposableDeviceActivity;

        private readonly IEnumerable<IRootDeviceInterface> _rootDeviceInterfaces;

        private IObservable<IHttpRequestResponse> _httpListenerObservable;

        private readonly bool _isClientsProvided;

        public ILogger Logger { get; set; }

        public IObservable<DeviceActivity> DeviceActivityObservable { get; }

        public bool IsStarted { get; private set; }

        private Device()
        {
            var deviceActivitySubject = new BehaviorSubject<DeviceActivity>(DeviceActivity.Initialized);

            _observerDeviceActivity = deviceActivitySubject.AsObserver();
            DeviceActivityObservable = deviceActivitySubject.AsObservable();
        }

        public Device(IRootDeviceConfiguration rootDeviceConfiguration) : this()
        {
            if (rootDeviceConfiguration?.IpEndPoint == null)
            {
                throw new SSDPException("At least one Root Device must be fully specified.");
            }
            var rootDeviceInterface = new RootDeviceInterface
            {
                RootDeviceConfiguration = rootDeviceConfiguration,
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

            rootDeviceInterface.UdpMulticastClient.Client.Bind(new IPEndPoint(rootDeviceConfiguration.IpEndPoint?.Address, UdpSSDPMulticastPort));


            rootDeviceInterface.UdpUnicastClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            rootDeviceInterface.UdpUnicastClient.Client.Bind(new IPEndPoint(rootDeviceConfiguration.IpEndPoint.Address, rootDeviceConfiguration.IpEndPoint.Port));

            _rootDeviceInterfaces = new List<IRootDeviceInterface> {rootDeviceInterface};

        }

        public Device(params IRootDeviceInterface[] rootDeviceInterfaces) : this()
        {
            _rootDeviceInterfaces = rootDeviceInterfaces;

            _isClientsProvided = true;
        }

        public async Task HotStartAsync(IObservable<IHttpRequestResponse> httpListenerObservable)
        {
            _httpListenerObservable = httpListenerObservable;

            await StartAsync();
        }

        public async Task StartAsync(CancellationToken ct)
        {
            if (!_rootDeviceInterfaces?.Any() ?? _rootDeviceInterfaces is null)
            {
                throw new SSDPException("No Root Device interfaces specified.");
            }

            foreach (var rootDevice in _rootDeviceInterfaces)
            {
                if (_httpListenerObservable == null)
                {
                    _httpListenerObservable = rootDevice.UdpMulticastClient
                        .ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError)
                        .Publish().RefCount();
                }
                else
                {
                    _httpListenerObservable = _httpListenerObservable.Merge(
                        rootDevice.UdpMulticastClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError))
                        .Publish().RefCount();
                }

                if (_httpListenerObservable == null)
                {
                    _httpListenerObservable = rootDevice.UdpUnicastClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError)
                        .Publish().RefCount();
                }
                else
                {
                    _httpListenerObservable = _httpListenerObservable.Merge(
                        rootDevice.UdpUnicastClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError))
                        .Publish().RefCount();
                }

                await StartAsync();
            }
        }

        private async Task StartAsync()
        {
            var mSearchDeviceRequestHandler = new MSearchDeviceRequestHandler(
                _rootDeviceInterfaces,
                _observerDeviceActivity,
                Logger);

            _disposableDeviceActivity = mSearchDeviceRequestHandler.
                MSearchRequestObservable(_httpListenerObservable)
                .Finally(mSearchDeviceRequestHandler.Dispose)
                .Subscribe(
                    _ =>
                    {

                    },
                    ex =>
                    {

                    },
                    () =>
                    {

                    });

            IsStarted = true;

            await SendAliveAsync();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        private async Task SendAliveAsync()
        {
            foreach (var rootDeviceInterface in _rootDeviceInterfaces)
            {
                var notifications = GetAllEntities(rootDeviceInterface)
                    .Select(ent =>
                    {
                        var rootConfiguration = rootDeviceInterface.RootDeviceConfiguration;

                        var searchPort = ((IPEndPoint) rootDeviceInterface.UdpUnicastClient.Client.LocalEndPoint)?.Port ?? 1900;

                        return new Notify
                        {
                            NotifyTransportType = TransportType.Multicast,
                            HOST = $"{UdpSSDPMultiCastAddress}:{UdpSSDPMulticastPort}",
                            CacheControl = rootConfiguration.CacheControl,
                            Location = rootConfiguration.Location,
                            NT = rootConfiguration.ToUri(),
                            NTS = NTS.Alive,
                            Server = rootConfiguration.Server,
                            USN = (rootConfiguration as IUSN).ToUri(),
                            BOOTID = DateTime.Now.FromUnixTime(),
                            CONFIGID = rootConfiguration.CONFIGID,
                            SEARCHPORT = searchPort,
                            SECURELOCATION = rootConfiguration.SecureLocation.AbsoluteUri,
                        };
                    });

                foreach (var notify in notifications)
                {
                    await SendNotifyAsync(notify, rootDeviceInterface.RootDeviceConfiguration.IpEndPoint);
                }

            }
        }

        public async Task SendNotifyAsync(INotify notifySsdp, IPEndPoint ipEndPoint)
        {
            var rootDeviceInterface =
                _rootDeviceInterfaces.FirstOrDefault(d => Equals(d.UdpMulticastClient.Client.LocalEndPoint, ipEndPoint));

            if (rootDeviceInterface == null)
            {
                throw new SSDPException($"End Point not available: {ipEndPoint.Address}:{ipEndPoint.Port}");
            }

            await SendNotiFyAsync(rootDeviceInterface, notifySsdp);
        }

        private async Task SendNotiFyAsync(IRootDeviceInterface rootDeviceInterface, INotify notify)
        {
            // Insert random delay according to UPnP 2.0 spec. section 1.2.1 (page 27).
            var wait = new Random();
            await Task.Delay(TimeSpan.FromMilliseconds(wait.Next(50, 100)));

            // According to the UPnP spec the UDP Multicast Notify should be send three times
            for (var i = 0; i < 3; i++)
            {
                var datagram = ComposeNotifyDatagram(notify);
                await rootDeviceInterface.UdpMulticastClient.SendAsync(datagram, datagram.Length, rootDeviceInterface.RootDeviceConfiguration.IpEndPoint);
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

        private static byte[] ComposeNotifyDatagram(INotify notify)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append("NOTIFY * HTTP/1.1\r\n");

            stringBuilder.Append(notify.NotifyTransportType == TransportType.Multicast
                ? "HOST: 239.255.255.250:1900\r\n"
                : $"HOST: {notify.HOST}\r\n");

            if (notify.NTS == NTS.Alive)
            {
                stringBuilder.Append($"CACHE-CONTROL: max-age = {notify.CacheControl.TotalSeconds}\r\n");
            }

            if (notify.NTS == NTS.Alive || notify.NTS == NTS.Update)
            {
                stringBuilder.Append($"LOCATION: {notify.Location.AbsoluteUri}\r\n");
            }

            stringBuilder.Append($"NT: {notify.NT}\r\n");
            stringBuilder.Append($"NTS: {notify.NTS.ToUri()}\r\n");

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
                HeaderHelper.AddOptionalHeader(stringBuilder, "SEARCHPORT.UPNP.ORG", notify.SEARCHPORT.ToString());
                HeaderHelper.AddOptionalHeader(stringBuilder, "SECURELOCATION.UPNP.ORG", notify.SECURELOCATION);
            }

            // Adding additional vendor specific headers if such are specified
            if (notify.Headers?.Any() ?? false)
            {
                foreach (var header in notify.Headers)
                {
                    stringBuilder.Append($"{header.Key}: {header.Value}\r\n");
                }
            }

            stringBuilder.Append("\r\n");

            return Encoding.UTF8.GetBytes(stringBuilder.ToString());
        }



        public void Dispose()
        {
            _disposableDeviceActivity?.Dispose();

            if (_isClientsProvided)
            {
                return;
            }
            else
            {
                foreach (var client in _rootDeviceInterfaces)
                {
                    client?.UdpMulticastClient?.Client?.Dispose();
                    client?.UdpUnicastClient?.Client.Dispose();
                }
            }
        }
    }
}