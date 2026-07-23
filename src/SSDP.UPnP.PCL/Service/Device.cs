using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SimpleHttpListener.Rx;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Enum;
using SSDP.UPnP.PCL.ExtensionMethod;
using SSDP.UPnP.PCL.Handler;
using SSDP.UPnP.PCL.Helper;
using SSDP.UPnP.PCL.Interfaces.Model;
using SSDP.UPnP.PCL.Interfaces.Service;
using SSDP.UPnP.PCL.Model;
using SSDP.UPnP.PCL.Rx;
using SSDP.UPnP.PCL.Service.Base;
using static SSDP.UPnP.PCL.Helper.Constants;

namespace SSDP.UPnP.PCL.Service
{
    public class Device : EntityBase, IDevice
    {
        // Per UDA 2.0 the response delay must be spread over the M-SEARCH MX value,
        // and MX must be treated as at most 5 seconds.
        private static readonly TimeSpan MaxResponseDelay = TimeSpan.FromSeconds(5);

        private readonly BehaviorSubject<DeviceActivity> _deviceActivitySubject;

        private IDisposable _disposableDeviceActivity;

        private readonly IEnumerable<IRootDeviceInterface> _rootDeviceInterfaces;

        private IObservable<HttpRequestResponse> _httpListenerObservable;

        private readonly bool _isClientsProvided;

        private bool _skipAlive;

        public ILogger Logger { get; set; }

        public IObservable<DeviceActivity> DeviceActivityObservable { get; }

        public bool IsStarted { get; private set; }

        private Device()
        {
            _deviceActivitySubject = new BehaviorSubject<DeviceActivity>(DeviceActivity.Initialized);
            DeviceActivityObservable = _deviceActivitySubject.AsObservable();
        }

        public Device(IRootDeviceConfiguration rootDeviceConfiguration) : this()
        {
            if (rootDeviceConfiguration?.IpEndPoint is null)
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

            rootDeviceInterface.UdpMulticastClient.Client.Bind(new IPEndPoint(rootDeviceConfiguration.IpEndPoint.Address, UdpSSDPMulticastPort));

            rootDeviceInterface.UdpMulticastClient.JoinMulticastGroup(IPAddress.Parse(UdpSSDPMultiCastAddress), rootDeviceConfiguration.IpEndPoint.Address);

            if (rootDeviceConfiguration.IpEndPoint.Port != UdpSSDPMulticastPort)
            {
                rootDeviceInterface.UdpUnicastClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                rootDeviceInterface.UdpUnicastClient.Client.Bind(rootDeviceConfiguration.IpEndPoint);
            }
            else
            {
                rootDeviceInterface.UdpUnicastClient = rootDeviceInterface.UdpMulticastClient;
            }

            _rootDeviceInterfaces = new List<IRootDeviceInterface> { rootDeviceInterface };
        }

        public Device(params IRootDeviceInterface[] rootDeviceInterfaces) : this()
        {
            if (rootDeviceInterfaces is null || rootDeviceInterfaces.Length == 0)
            {
                throw new SSDPException("At least one Root Device Interface must be specified.");
            }

            _rootDeviceInterfaces = rootDeviceInterfaces;

            foreach (var rootNode in rootDeviceInterfaces)
            {
                if (rootNode.UdpUnicastClient is not null)
                {
                    ((RootDeviceConfiguration)rootNode.RootDeviceConfiguration).IpEndPoint = rootNode.UdpUnicastClient.Client.LocalEndPoint as IPEndPoint;
                }
                else if (rootNode.UdpMulticastClient is not null)
                {
                    ((RootDeviceConfiguration)rootNode.RootDeviceConfiguration).IpEndPoint = rootNode.UdpMulticastClient.Client.LocalEndPoint as IPEndPoint;
                }
                else
                {
                    throw new SSDPException("No UDP Client specified for interface.");
                }
            }

            _isClientsProvided = true;
        }

        internal async Task HotStartAsync(IObservable<HttpRequestResponse> httpListenerObservable, bool skipAlive)
        {
            _skipAlive = skipAlive;

            await HotStartAsync(httpListenerObservable);
        }

        public async Task HotStartAsync(IObservable<HttpRequestResponse> httpListenerObservable)
        {
            _httpListenerObservable = httpListenerObservable;

            await StartAsync();
        }

        public async Task StartAsync(CancellationToken ct)
        {
            if (_rootDeviceInterfaces is null || !_rootDeviceInterfaces.Any())
            {
                throw new SSDPException("No Root Device interfaces specified.");
            }

            var listenerObservables = new List<IObservable<HttpRequestResponse>>();

            foreach (var rootDevice in _rootDeviceInterfaces)
            {
                if (rootDevice.UdpMulticastClient is not null)
                {
                    listenerObservables.Add(
                        rootDevice.UdpMulticastClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError));
                }

                if (rootDevice.UdpUnicastClient is not null
                    && rootDevice.UdpUnicastClient != rootDevice.UdpMulticastClient)
                {
                    listenerObservables.Add(
                        rootDevice.UdpUnicastClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError));
                }
            }

            if (listenerObservables.Count == 0)
            {
                throw new SSDPException("No UDP Client defined for any Root Device interface.");
            }

            _httpListenerObservable = listenerObservables
                .Merge()
                .Publish()
                .RefCount();

            await StartAsync();
        }

        private async Task StartAsync()
        {
            var mSearchDeviceRequestHandler = new MSearchDeviceRequestHandler(
                _rootDeviceInterfaces,
                Logger);

            _disposableDeviceActivity = mSearchDeviceRequestHandler
                .MSearchRequestObservable(_httpListenerObservable)
                .SelectMany(async x =>
                {
                    await SendMSearchResponseAsync(x.RootDeviceInterface, x.Response);
                    return x;
                })
                .FinallyAsync(async () => { await SendByeByeAsync(); })
                .Finally(mSearchDeviceRequestHandler.Dispose)
                .Subscribe(
                    _ => { },
                    ex => Logger?.LogError(ex, "SSDP device listener terminated unexpectedly."));

            IsStarted = true;

            if (_skipAlive)
            {
                return;
            }

            await SendAliveAsync();
        }

        public async Task UpdateAsync()
        {
            await SendUpdateAsync();
        }

        public async Task ByeByeAsync()
        {
            await SendByeByeAsync();
        }

        // Responds to an M-SEARCH request with a unicast UDP datagram sent to the
        // requester, after a random delay spread over the request's MX value (UDA 2.0
        // section 1.3.3).
        private async Task SendMSearchResponseAsync(
            IRootDeviceInterface rootDeviceInterface,
            IMSearchResponse response)
        {
            if (response.RemoteIpEndPoint is null || rootDeviceInterface.UdpUnicastClient is null)
            {
                return;
            }

            _deviceActivitySubject.OnNext(DeviceActivity.Responding);

            var maxDelay = response.MX > TimeSpan.Zero && response.MX < MaxResponseDelay
                ? response.MX
                : MaxResponseDelay;

            await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * maxDelay.TotalMilliseconds));

            var datagram = ComposeMSearchResponseDatagram(response);

            await rootDeviceInterface.UdpUnicastClient.SendAsync(datagram, datagram.Length, response.RemoteIpEndPoint);
        }

        private async Task SendUpdateAsync()
        {
            foreach (var rootDeviceInterface in _rootDeviceInterfaces)
            {
                var notifications = GetAllDevices(rootDeviceInterface)
                    .SelectMany(device =>
                    {
                        var rootConfiguration = rootDeviceInterface.RootDeviceConfiguration;

                        var searchPort = (uint)((rootDeviceInterface.UdpUnicastClient.Client.LocalEndPoint as IPEndPoint)?.Port ?? UdpSSDPMulticastPort);

                        var nextBootId = (uint)DateTime.UtcNow.FromUnixTime();

                        var entities = new List<IEntity> { device };

                        if (device.Services?.Any() ?? false)
                        {
                            entities.AddRange(device.Services);
                        }

                        var notifyList = entities.Select(CreateNotify).ToList();

                        ((DeviceConfiguration)device).BOOTID = nextBootId;

                        return notifyList;

                        // Local function
                        Notify CreateNotify(IEntity entity)
                        {
                            var usn = new USN
                            {
                                TypeName = entity.TypeName,
                                EntityType = entity.EntityType,
                                Domain = entity.Domain,
                                Version = entity.Version,
                                DeviceUUID = device.DeviceUUID
                            };

                            return new Notify
                            {
                                NotifyTransportType = TransportType.Multicast,
                                HOST = $"{UdpSSDPMultiCastAddress}:{UdpSSDPMulticastPort}",
                                Location = rootConfiguration.Location,
                                NT = entity.ToUri(),
                                NTS = NTS.Update,
                                USN = usn,
                                BOOTID = device.BOOTID,
                                CONFIGID = rootConfiguration.CONFIGID,
                                NEXTBOOTID = nextBootId,
                                SEARCHPORT = searchPort,
                            };
                        }
                    });

                foreach (var notify in notifications)
                {
                    await SendNotifyAsync(notify, rootDeviceInterface.RootDeviceConfiguration.IpEndPoint);
                }
            }
        }

        private async Task SendByeByeAsync()
        {
            foreach (var rootDeviceInterface in _rootDeviceInterfaces)
            {
                var notifications = GetAllDevices(rootDeviceInterface)
                    .SelectMany(device =>
                    {
                        var rootConfiguration = rootDeviceInterface.RootDeviceConfiguration;

                        var entities = new List<IEntity> { device };

                        if (device.Services?.Any() ?? false)
                        {
                            entities.AddRange(device.Services);
                        }

                        return entities.Select(CreateNotify);

                        // Local function
                        Notify CreateNotify(IEntity entity)
                        {
                            var usn = new USN
                            {
                                TypeName = entity.TypeName,
                                EntityType = entity.EntityType,
                                Domain = entity.Domain,
                                Version = entity.Version,
                                DeviceUUID = device.DeviceUUID
                            };

                            return new Notify
                            {
                                NotifyTransportType = TransportType.Multicast,
                                HOST = $"{UdpSSDPMultiCastAddress}:{UdpSSDPMulticastPort}",
                                NT = entity.ToUri(),
                                NTS = NTS.ByeBye,
                                USN = usn,
                                BOOTID = device.BOOTID,
                                CONFIGID = rootConfiguration.CONFIGID,
                            };
                        }
                    });

                foreach (var notify in notifications)
                {
                    await SendNotifyAsync(notify, rootDeviceInterface.RootDeviceConfiguration.IpEndPoint);
                }
            }
        }

        private async Task SendAliveAsync()
        {
            foreach (var rootDeviceInterface in _rootDeviceInterfaces)
            {
                var notifications = GetAllDevices(rootDeviceInterface)
                    .SelectMany(device =>
                    {
                        var rootConfiguration = rootDeviceInterface.RootDeviceConfiguration;

                        var searchPort = (uint)((rootDeviceInterface.UdpUnicastClient.Client.LocalEndPoint as IPEndPoint)?.Port ?? UdpSSDPMulticastPort);

                        var entities = new List<IEntity> { device };

                        if (device.Services?.Any() ?? false)
                        {
                            entities.AddRange(device.Services);
                        }

                        return entities.Select(CreateNotify);

                        // Local function
                        Notify CreateNotify(IEntity entity)
                        {
                            var usn = new USN
                            {
                                TypeName = entity.TypeName,
                                EntityType = entity.EntityType,
                                Domain = entity.Domain,
                                Version = entity.Version,
                                DeviceUUID = device.DeviceUUID
                            };

                            return new Notify
                            {
                                NotifyTransportType = TransportType.Multicast,
                                HOST = $"{UdpSSDPMultiCastAddress}:{UdpSSDPMulticastPort}",
                                CacheControl = rootConfiguration.CacheControl,
                                Location = rootConfiguration.Location,
                                NT = entity.ToUri(),
                                NTS = NTS.Alive,
                                Server = rootConfiguration.Server,
                                USN = usn,
                                BOOTID = device.BOOTID,
                                CONFIGID = rootConfiguration.CONFIGID,
                                SEARCHPORT = searchPort,
                                SECURELOCATION = rootConfiguration.SecureLocation?.AbsoluteUri,
                            };
                        }
                    });

                foreach (var notify in notifications)
                {
                    await SendNotifyAsync(notify, rootDeviceInterface.RootDeviceConfiguration.IpEndPoint);
                }
            }
        }

        public async Task SendNotifyAsync(INotify notifySsdp, IPEndPoint ipEndPoint)
        {
            var rootDeviceInterface = _rootDeviceInterfaces?.FirstOrDefault(i => i.IsMatchingInterface(ipEndPoint));

            if (rootDeviceInterface is null)
            {
                throw new SSDPException($"End Point not available: {ipEndPoint.Address}:{ipEndPoint.Port}");
            }

            await SendNotifyAsync(rootDeviceInterface, notifySsdp);
        }

        private async Task SendNotifyAsync(IRootDeviceInterface rootDeviceInterface, INotify notify)
        {
            _deviceActivitySubject.OnNext(DeviceActivity.Notifying);

            // Insert random delay according to UPnP 2.0 spec. section 1.2.1 (page 27).
            await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(50, 100)));

            // According to the UPnP spec the UDP Multicast Notify should be send three times
            for (var i = 0; i < 3; i++)
            {
                var datagram = ComposeNotifyDatagram(notify);
                await rootDeviceInterface.UdpMulticastClient
                    .SendAsync(datagram, datagram.Length, UdpSSDPMultiCastAddress, UdpSSDPMulticastPort);
                // Random delay between resends of 200 - 400 milliseconds.
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(200, 400)));
            }
        }

        internal static byte[] ComposeMSearchResponseDatagram(IMSearchResponse response)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append($"HTTP/1.1 {response.StatusCode} {response.ResponseReason}\r\n");
            stringBuilder.Append($"CACHE-CONTROL: max-age={(int)response.CacheControl.TotalSeconds}\r\n");
            stringBuilder.Append($"DATE: {DateTime.UtcNow:r}\r\n");
            stringBuilder.Append("EXT:\r\n");
            stringBuilder.Append($"LOCATION: {response.Location}\r\n");
            stringBuilder.Append($"SERVER: " +
                                 $"{response.Server.OperatingSystem}/{response.Server.OperatingSystemVersion}" +
                                 $" " +
                                 $"UPnP/{response.Server.UpnpMajorVersion}.{response.Server.UpnpMinorVersion}" +
                                 $" " +
                                 $"{response.Server.ProductName}/{response.Server.ProductVersion}\r\n");
            stringBuilder.Append($"ST: {response.ST.ToUri()}\r\n");
            stringBuilder.Append($"USN: {response.USN.ToUri()}\r\n");
            stringBuilder.Append($"BOOTID.UPNP.ORG: {response.BOOTID}\r\n");

            HeaderHelper.AddOptionalHeader(stringBuilder, "CONFIGID.UPNP.ORG", response.CONFIGID.ToString());

            if (response.SEARCHPORT > 0)
            {
                HeaderHelper.AddOptionalHeader(stringBuilder, "SEARCHPORT.UPNP.ORG", response.SEARCHPORT.ToString());
            }

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

            return Encoding.UTF8.GetBytes(stringBuilder.ToString());
        }

        internal static byte[] ComposeNotifyDatagram(INotify notify)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.Append("NOTIFY * HTTP/1.1\r\n");

            stringBuilder.Append(notify.NotifyTransportType == TransportType.Multicast
                ? $"HOST: {UdpSSDPMultiCastAddress}:{UdpSSDPMulticastPort}\r\n"
                : $"HOST: {notify.HOST}\r\n");

            if (notify.NTS == NTS.Alive)
            {
                stringBuilder.Append($"CACHE-CONTROL: max-age={(int)notify.CacheControl.TotalSeconds}\r\n");
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
                                     $"{notify.Server.OperatingSystem}/{notify.Server.OperatingSystemVersion}" +
                                     $" " +
                                     $"UPnP/{notify.Server.UpnpMajorVersion}.{notify.Server.UpnpMinorVersion}" +
                                     $" " +
                                     $"{notify.Server.ProductName}/{notify.Server.ProductVersion}\r\n");
            }

            stringBuilder.Append($"USN: {notify.USN.ToUri()}\r\n");

            stringBuilder.Append($"BOOTID.UPNP.ORG: {notify.BOOTID}\r\n");
            stringBuilder.Append($"CONFIGID.UPNP.ORG: {notify.CONFIGID}\r\n");

            if (notify.NTS == NTS.Update)
            {
                stringBuilder.Append($"NEXTBOOTID.UPNP.ORG: {notify.NEXTBOOTID}\r\n");
            }

            if (notify.NTS == NTS.Alive || notify.NTS == NTS.Update)
            {
                if (notify.SEARCHPORT > 0 && notify.SEARCHPORT != UdpSSDPMulticastPort)
                {
                    HeaderHelper.AddOptionalHeader(stringBuilder, "SEARCHPORT.UPNP.ORG", notify.SEARCHPORT.ToString());
                }

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

            _deviceActivitySubject.OnCompleted();
            _deviceActivitySubject.Dispose();

            if (!_isClientsProvided)
            {
                foreach (var client in _rootDeviceInterfaces)
                {
                    client?.UdpMulticastClient?.Dispose();

                    if (client?.UdpUnicastClient != client?.UdpMulticastClient)
                    {
                        client?.UdpUnicastClient?.Dispose();
                    }
                }
            }
        }
    }
}
