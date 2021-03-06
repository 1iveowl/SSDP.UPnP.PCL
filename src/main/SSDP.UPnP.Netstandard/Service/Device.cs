﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
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
using SSDP.UPnP.PCL.Rx;
using SSDP.UPnP.PCL.Service.Base;
using static SSDP.UPnP.PCL.Helper.Constants;

[assembly: InternalsVisibleTo("SSDP.Device.xUnit")]
namespace SSDP.UPnP.PCL.Service
{
    public class Device : EntityBase, IDevice
    {
        private readonly IObserver<DeviceActivity> _observerDeviceActivity;

        private IDisposable _disposableDeviceActivity;

        private readonly IEnumerable<IRootDeviceInterface> _rootDeviceInterfaces;

        private IObservable<IHttpRequestResponse> _httpListenerObservable;

        private readonly bool _isClientsProvided;

#if DEBUG
        private bool _skipAlive;
#endif

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

            rootDeviceInterface.UdpMulticastClient.Client.Bind(new IPEndPoint(rootDeviceConfiguration.IpEndPoint?.Address, UdpSSDPMulticastPort));

            rootDeviceInterface.UdpMulticastClient.JoinMulticastGroup(IPAddress.Parse(UdpSSDPMultiCastAddress), rootDeviceConfiguration.IpEndPoint?.Address);

            if (rootDeviceConfiguration.IpEndPoint.Port != UdpSSDPMulticastPort)
            {
                rootDeviceInterface.UdpUnicastClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                rootDeviceInterface.UdpUnicastClient.Client.Bind(rootDeviceConfiguration.IpEndPoint);
            }
            else
            {
                rootDeviceInterface.UdpUnicastClient = rootDeviceInterface.UdpMulticastClient;
            }

            _rootDeviceInterfaces = new List<IRootDeviceInterface> {rootDeviceInterface};

        }

        public Device(params IRootDeviceInterface[] rootDeviceInterfaces) : this()
        {
            _rootDeviceInterfaces = rootDeviceInterfaces;

            foreach (var rootNode in rootDeviceInterfaces)
            {
                if (!(rootNode.UdpUnicastClient is null))
                {
                    ((RootDeviceConfiguration)rootNode.RootDeviceConfiguration).IpEndPoint = rootNode.UdpUnicastClient.Client.LocalEndPoint as IPEndPoint;
                }
                else if (!(rootNode.UdpMulticastClient is null))
                {
                    ((RootDeviceConfiguration)rootNode.RootDeviceConfiguration).IpEndPoint = rootNode.UdpMulticastClient.Client.LocalEndPoint as IPEndPoint;
                }
                else
                {
                    throw new SSDPException($"No UDP Client specified for interface.");
                }
            }

            _isClientsProvided = true;
        }

#if DEBUG
        internal async Task HotStartAsync(IObservable<IHttpRequestResponse> httpListenerObservable, bool skipAlive)
        {
            _skipAlive = skipAlive;

            await HotStartAsync(httpListenerObservable);
        }
#endif

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
                        .ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError);
                }
                else
                {
                    _httpListenerObservable = _httpListenerObservable.Merge(
                        rootDevice.UdpMulticastClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError));
                }

                if (rootDevice.UdpMulticastClient != rootDevice.UdpUnicastClient)
                {
                    if (_httpListenerObservable == null)
                    {
                        _httpListenerObservable = rootDevice.UdpUnicastClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError);
                    }
                    else
                    {
                        _httpListenerObservable = _httpListenerObservable.Merge(
                                rootDevice.UdpUnicastClient.ToHttpListenerObservable(ct, ErrorCorrection.HeaderCompletionError));
                    }
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
                .FinallyAsync(async () => { await SendByeByeAsync(); })
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

#if DEBUG
            if (_skipAlive)
            {
                return;
            }
#endif

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

        private async Task SendUpdateAsync()
        {
            foreach (var rootDeviceInterface in _rootDeviceInterfaces)
            {
                var notifications = GetAllDevices(rootDeviceInterface)
                    .Where(device => !(device is null))
                    .SelectMany(device =>
                    {
                        var notifyList = new List<Notify>();

                        var rootConfiguration = rootDeviceInterface.RootDeviceConfiguration;

                        var searchPort = (uint) (((IPEndPoint)rootDeviceInterface.UdpUnicastClient.Client.LocalEndPoint)?.Port ?? 1900);

                        var nextBootId = (uint) DateTime.Now.FromUnixTime();

                        notifyList.Add(CreateNotify(device));
                        
                        if (device?.Services?.Any() ?? false)
                        {
                            notifyList.AddRange(device.Services.Select(CreateNotify));
                        }
                        
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
                                NT = device.ToUri(),
                                NTS = NTS.Update,
                                USN = usn,
                                BOOTID = device.BOOTID,
                                CONFIGID = rootConfiguration.CONFIGID,
                                NEXTBOOTID = nextBootId,
                                SEARCHPORT = searchPort,
                            };
                        };
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
                    .Where(device => !(device is null))
                    .SelectMany(device =>
                    {
                        var notifyList = new List<Notify>();

                        var rootConfiguration = rootDeviceInterface.RootDeviceConfiguration;

                        notifyList.Add(CreateNotify(device));

                        if (device?.Services?.Any() ?? false)
                        {
                            notifyList.AddRange(device.Services.Select(CreateNotify));
                        }

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
                                NT = device.ToUri(),
                                NTS = NTS.ByeBye,
                                USN = usn,
                                BOOTID = device.BOOTID,
                                CONFIGID = rootConfiguration.CONFIGID,
                            };
                        };
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
                    .Where(device => !(device is null))
                    .SelectMany(device =>
                    {
                        var notifyList = new List<Notify>();

                        var rootConfiguration = rootDeviceInterface.RootDeviceConfiguration;

                        var searchPort = (uint)(((IPEndPoint) rootDeviceInterface.UdpUnicastClient.Client.LocalEndPoint)?.Port ?? 1900);
                        
                        notifyList.Add(CreateNotify(device));

                        if (device?.Services?.Any() ?? false)
                        {
                            notifyList.AddRange(device.Services.Select(CreateNotify));
                        }
                        
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
                                CacheControl = rootConfiguration.CacheControl,
                                Location = rootConfiguration.Location,
                                NT = rootConfiguration.ToUri(),
                                NTS = NTS.Alive,
                                Server = rootConfiguration.Server,
                                USN = usn,
                                BOOTID = device.BOOTID,
                                CONFIGID = rootConfiguration.CONFIGID,
                                SEARCHPORT = searchPort,
                                SECURELOCATION = rootConfiguration.SecureLocation?.AbsoluteUri,
                            };
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
            var rootDeviceInterface = _rootDeviceInterfaces?.FirstOrDefault(i => i.IsMatchingInterface(ipEndPoint));

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
                await rootDeviceInterface.UdpMulticastClient
                    .SendAsync(datagram, datagram.Length, UdpSSDPMultiCastAddress, UdpSSDPMulticastPort);
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

            HeaderHelper.AddOptionalHeader(stringBuilder, "CONFIGID.UPNP.ORG", response.CONFIGID.ToString());
            HeaderHelper.AddOptionalHeader(stringBuilder, "SEARCHPORT.UPNP.ORG", response.SEARCHPORT.ToString());
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

            stringBuilder.Append($"USN: {notify?.USN.ToUri()}\r\n");
            Debug.WriteLine(notify?.USN.ToUri());
                
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

            if (!_isClientsProvided)
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