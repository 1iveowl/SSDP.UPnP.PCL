# SSDP.UPnP.PCL

[![NuGet](https://img.shields.io/nuget/v/SSDP.UPnP.PCL?logo=nuget&label=SSDP.UPnP.PCL)](https://www.nuget.org/packages/SSDP.UPnP.PCL)
[![Downloads](https://img.shields.io/nuget/dt/SSDP.UPnP.PCL?logo=nuget&color=blue)](https://www.nuget.org/packages/SSDP.UPnP.PCL)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](License.md)

[![.NET Standard](https://img.shields.io/badge/.NET%20Standard-2.0-5C2D91?logo=dotnet&logoColor=white)](https://learn.microsoft.com/dotnet/standard/net-standard)
[![System.Reactive](https://img.shields.io/badge/Rx-5.0.0-ff69b4.svg)](https://reactivex.io/)
[![UPnP](https://img.shields.io/badge/UPnP%20Device%20Architecture-2.0-2563EB.svg)](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf)

An Rx-based SSDP library for discovering and advertising UPnP Device Architecture 2.0 devices and services.

*Please star this project if you find it useful. Thank you.*

## Overview

This library supports version 2.0 of the UPnP Device Architecture. It uses [Reactive Extensions](https://reactivex.io/) because SSDP is an ongoing stream of discovery replies and notifications, a model that maps naturally to observables.

The library targets .NET Standard 2.0 and is intended for modern .NET-compatible platforms. It supports multi-homed control points and devices.

## Version 6.0

Version 6.0 improved reliability and stability throughout the library and introduced breaking changes. Prefer it over earlier releases when compatibility allows.

- `ControlPoint` is substantially more reliable and supports multi-homed use.
- `Device` also supports multi-homed use, but needs additional real-world testing; use it with appropriate caution.

## Get started with a control point

The example below creates a listener, sends an SSDP M-SEARCH discovery request, and observes M-SEARCH replies and UPnP `NOTIFY` messages on the local network.

> **Important**
> On Windows, the built-in SSDP Discovery service can receive these messages before your application does. If M-SEARCH responses or `NOTIFY` messages are missing, stop that service and check for other local SSDP listeners.

### Construct a control point

There are two construction options:

1. **IP address constructor:** supply one or more local IP addresses. More than one address creates a multi-homed control point.
2. **Interface constructor (advanced):** create one or more interfaces that implement `IControlPointInterface`, then pass them to the constructor.

The following example uses the IP address constructor.

### Start a control point

Start a control point with either:

1. `StartAsync`
2. `HotStartAsync` (advanced)

`StartAsync` creates the listeners using the construction parameters. `HotStartAsync` accepts an `IObservable<IHttpRequestResponse>` for advanced scenarios where the incoming stream is shared with another service, such as UPnP eventing.

The following example uses `StartAsync`.

### Control point example

The following example creates a control point for the selected local IP address. It listens for M-SEARCH responses and `NOTIFY` broadcasts, then writes both message types to a console application. In a production application, replace the console output with appropriate handling.

The example also broadcasts an SSDP M-SEARCH discovery request. UPnP devices on the local network should reply, and those replies appear in the listener output.


```csharp
class Program
{
    private static IControlPoint _controlPoint;
    private static IPAddress _controlPointLocalIp1;


   // For this test to work you most likely need to stop the SSDP Discovery service on Windows
    // If you don't stop the SSDP Windows Service, the service will intercept the UPnP multicasts and consequently nothing will show up in the console. 

    static async Task Main(string[] args)
    {
        if (args?.Any() ?? false)
        {
            var ipStr = args[0];

            if (IPAddress.TryParse(ipStr, out var ip))
            {
                _controlPointLocalIp1 = ip;
            }
        }

        if (_controlPointLocalIp1 is null)
        {
            _controlPointLocalIp1 = GetBestGuessLocalIPAddress();
        }
        
        System.Console.WriteLine($"IP Address: {_controlPointLocalIp1.ToString()}");

        var cts = new CancellationTokenSource();

        await StartAsync(cts.Token);

        System.Console.WriteLine("Press any key to end.");

        System.Console.ReadKey();

        cts.Cancel();

        System.Console.WriteLine("Press any key to exit.");
        System.Console.ReadKey();

    }

    private static async Task StartAsync(CancellationToken ct)
    {

        await StartControlPointListeningAsync(ct);
    }

    private static async Task StartControlPointListeningAsync(CancellationToken ct)
    {
        _controlPoint = new ControlPoint(_controlPointLocalIp1);

        _controlPoint.Start(ct);

        ListenToNotify();

        ListenToMSearchResponse(ct);
        
        await StartMSearchRequestMulticastAsync();
    }

        private static void ListenToNotify()
    {
        var counter = 0;

        var observerNotify = _controlPoint.NotifyObservable();

        var disposableNotify = observerNotify
            .Subscribe(
                n =>
                {
                    counter++;
                    System.Console.BackgroundColor = ConsoleColor.DarkBlue;
                    System.Console.ForegroundColor = ConsoleColor.White;
                    System.Console.WriteLine($"---### Control Point Received a NOTIFY - #{counter} ###---");
                    System.Console.ResetColor();
                    System.Console.WriteLine($"{n?.NotifyTransportType.ToString()}");
                    System.Console.WriteLine($"From: {n?.HOST}");
                    System.Console.WriteLine($"Location: {n?.Location?.AbsoluteUri}");
                    System.Console.WriteLine($"Cache-Control: max-age = {n.CacheControl}");
                    System.Console.WriteLine($"Server: " +
                                             $"{n?.Server?.OperatingSystem}/{n?.Server?.OperatingSystemVersion} " +
                                             $"UPNP/" +
                                             $"{n?.Server?.UpnpMajorVersion}.{n?.Server?.UpnpMinorVersion}" +
                                             $" " +
                                             $"{n?.Server?.ProductName}/{n?.Server?.ProductVersion}" +
                                             $" - ({n?.Server?.FullString})");
                    System.Console.WriteLine($"NT: {n?.NT}");
                    System.Console.WriteLine($"NTS: {n?.NTS}");
                    System.Console.WriteLine($"USN: {n?.USN?.ToUri()}");

                    if (n.BOOTID > 0)
                    {
                        System.Console.WriteLine($"BOOTID: {n.BOOTID}");
                    }
                
                    System.Console.WriteLine($"CONFIGID: {n.CONFIGID}");
                    
                    System.Console.WriteLine($"NEXTBOOTID: {n.NEXTBOOTID}");
                    System.Console.WriteLine($"SEARCHPORT: {n.SEARCHPORT}");
                    System.Console.WriteLine($"SECURELOCATION: {n.SECURELOCATION}");

                    if (n.Headers.Any())
                    {
                        System.Console.ForegroundColor = ConsoleColor.DarkYellow;
                        System.Console.WriteLine($"Additional Headers: {n.Headers.Count}");
                        foreach (var header in n.Headers)
                        {
                            System.Console.WriteLine($"{header.Key}: {header.Value}; ");
                        }

                        System.Console.ResetColor();
                    }

                    System.Console.WriteLine($"Is UPnP 2.0 compliant: {n.IsUuidUpnp2Compliant}");

                    if (n.HasParsingError)
                    {
                        System.Console.WriteLine($"Parsing errors: {n.HasParsingError}");
                    }

                    System.Console.WriteLine();
                });
    }

    private static void ListenToMSearchResponse(CancellationToken ct)
    {
        var mSearchResObs = _controlPoint.MSearchResponseObservable();

        var counter = 0;

        var disposableMSearchresponse = mSearchResObs
            .Subscribe(
                res =>
                {
                    counter++;
                    System.Console.BackgroundColor = ConsoleColor.DarkBlue;
                    System.Console.ForegroundColor = ConsoleColor.White;
                    System.Console.WriteLine($"---### Control Point Received a  M-SEARCH RESPONSE #{counter} ###---");
                    System.Console.ResetColor();
                    System.Console.WriteLine($"{res?.TransportType.ToString()}");
                    System.Console.WriteLine($"Status code: {res.StatusCode} {res.ResponseReason}");
                    System.Console.WriteLine($"Location: {res?.Location?.AbsoluteUri}");
                    System.Console.WriteLine($"Date: {res.Date.ToString(CultureInfo.CurrentCulture)}");
                    System.Console.WriteLine($"Cache-Control: max-age = {res.CacheControl}");
                    System.Console.WriteLine($"Server: " +
                                             $"{res?.Server?.OperatingSystem}/{res?.Server?.OperatingSystemVersion} " +
                                             $"UPNP/" +
                                             $"{res?.Server?.UpnpMajorVersion}.{res?.Server?.UpnpMinorVersion}" +
                                             $" " +
                                             $"{res?.Server?.ProductName}/{res?.Server?.ProductVersion}" +
                                             $" - ({res?.Server?.FullString})");
                    System.Console.WriteLine($"ST: {res?.ST?.STString}");
                    System.Console.WriteLine($"USN: {res.USN?.ToUri()}");
                    System.Console.WriteLine($"BOOTID.UPNP.ORG: {res?.BOOTID}");
                    System.Console.WriteLine($"CONFIGID.UPNP.ORG: {res?.CONFIGID}");
                    System.Console.WriteLine($"SEARCHPORT.UPNP.ORG: {res?.SEARCHPORT}");
                    System.Console.WriteLine($"SECURELOCATION: {res?.SECURELOCATION}");

                    if (res?.Headers?.Any() ?? false)
                    {
                        System.Console.ForegroundColor = ConsoleColor.DarkYellow;
                        System.Console.WriteLine($"Additional Headers: {res.Headers?.Count}");
                        foreach (var header in res.Headers)
                        {
                            System.Console.WriteLine($"{header.Key}: {header.Value}; ");
                        }

                        System.Console.ResetColor();
                    }

                    if (res.HasParsingError)
                    {
                        System.Console.WriteLine($"Parsing errors: {res.HasParsingError}");
                    }

                    System.Console.WriteLine();
                });
    }


    private static async Task StartMSearchRequestMulticastAsync()
    {
        var mSearchMessage = new MSearch
        {
            TransportType = TransportType.Multicast,
            CPFN = "TestXamarin",

            Name = UdpSSDPMultiCastAddress,
            Port = UdpSSDPMulticastPort,
            MX = TimeSpan.FromSeconds(5),
            TCPPORT = TcpResponseListenerPort.ToString(),
            //ST = new ST("urn:myharmony-com:device:harmony:1"),
            ST = new ST
            {
                StSearchType = STType.All
            },
            //ST = new ST
            //{
            //    STtype  = STtype.ServiceType,
            //    Type = "SwitchPower",
            //    Version = "1",
            //    HasDomain = false
            //},
            //ST = new ST
            //{
            //    StSearchType = STSearchType.DomainDeviceSearch,
            //    Domain = "myharmony-com", 
            //    DeviceType = "harmony",
            //    Version = "1",
            //    //STtype = STtype.DeviceType,
            //    ////DeviceUUID = "myharmony-com:device:harmony:1",
            //    //Type = "harmony",
            //    //Version = "1",
            //    //HasDomain = true,
            //    //DomainName = "myharmony-com"
            //},

            UserAgent = new UserAgent
            {
                OperatingSystem = "Windows",
                OperatingSystemVersion = "10.0",
                ProductName = "SSDP.UPNP.PCL",
                ProductVersion = "0.9",
                UpnpMajorVersion = "2",
                UpnpMinorVersion = "0",
            }
        };

        await _controlPoint.SendMSearchAsync(mSearchMessage, _controlPointLocalIp1);
    }
}

```

### Search for UPnP devices

The [UPnP Device Architecture 2.0 specification](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf) defines the required M-SEARCH `ST` (search target) field. It contains one URI and must be one of the following:

* `ssdp:all` Search for all devices and services. 
* `upnp:rootdevice` Search for root devices only. 
* `uuid:device-UUID` Search for a particular vendor-defined device UUID.
* `urn:schemas-upnp-org:device:deviceType:ver` Search for a standard UPnP device type.
* `urn:schemas-upnp-org:service:serviceType:ver` Search for a standard UPnP service type.
* `urn:domain-name:device:deviceType:ver` Search for a vendor-defined device type.
* `urn:domain-name:service:serviceType:ver` Search for a vendor-defined service type.

> **Important**
> Create your own M-SEARCH request type that implements `IMSearchRequest`. Its implementation can be as simple or as specialized as your application requires.
```csharp
    internal class MSearch : IMSearchRequest
    {
        public bool InvalidRequest { get; } = false;
        public bool HasParsingError { get; internal set; }
        public string Name { get; internal set; }
        public int Port { get; internal set; }
        public IDictionary<string, string> Headers { get; internal set; }
        public TransportType TransportType { get; internal set; }
        public string MAN { get; internal set; }
        public string HOST { get; internal set; }
        public TimeSpan MX { get; internal set; }
        public IST ST { get; internal set; }
        public IUserAgent UserAgent { get; internal set; }
        public string CPFN { get; internal set; }
        public string CPUUID { get; internal set; }
        public int SEARCHPORT { get; internal set; }
        public string TCPPORT { get; internal set; }
        public IPEndPoint LocalIpEndPoint { get; internal set; }
        public IPEndPoint RemoteIpEndPoint { get; internal set; }
    }
```

## Device (beta)

The UPnP 2.0 device implementation is still a work in progress. Use it with care.

### Construct a device

There are two construction options:

1. **Root device configuration:** create an `IRootDeviceConfiguration`. The library supplies an implementation.
2. **Root device interface (advanced):** create one or more `IRootDeviceInterface` instances. Multiple interfaces create a multi-homed device, and you supply the UDP clients. This is useful when a UDP stream is shared with a control point or UPnP eventing service.

The following example uses a root device configuration.

### Start a device

As with `ControlPoint`, start a device with either:

1. `Start`
2. `HotStart` (advanced)

`Start` creates listeners from the construction parameters. `HotStart` accepts an `IObservable<IHttpRequestResponse>` for advanced scenarios where the incoming stream is shared with another service, such as UPnP eventing.

The following example uses `Start`.

```csharp
class Program
{
    private static IDevice _device;

    private static IPEndPoint _localMulticastIpEndPoint;

    // For this test to work you most likely need to stop the SSDP Discovery service on Windows
    // If you don't stop the SSDP Windows Service, the service will intercept the UPnP multicasts and consequently nothing will show up in the console. 

    static async Task Main(string[] args)
    {
        _localUnicastIpEndPoint = new IPEndPoint(IPAddress.Parse("[Your IP Address]"), 1901);

        _deviceLocalIpAddress = IPAddress.Parse("[Your IP Address]");


        var cts = new CancellationTokenSource();

        await StartAsync(cts.Token);
      
        System.Console.ReadKey();
    }

    private static async Task StartAsync(CancellationToken ct)
    {
        await StartDeviceListening();
    }

    private static async Task StartDeviceListening()
    {
        var rootDevice = 

        _device = new Device(CreateRootDevice());
        
        var cts = new CancellationTokenSource();

        await _device.StartAsync(cts.Token);

        System.Console.WriteLine("Press any key to bye bye...");
        System.Console.ReadLine();

        await _device.ByeByeAsync();

        _device?.Dispose();
    }

    private static IRootDeviceConfiguration CreateRootDevice()
    {
        return new RootDeviceConfiguration
        {
            DeviceUUID = Guid.NewGuid().ToString(),
            CacheControl = TimeSpan.FromSeconds(30),
            Location = new Uri("http://[Your IP Address]/device"),
            Server = new Server
            {
                OperatingSystem = "Windows",
                OperatingSystemVersion = "10",
                UpnpMajorVersion = "2",
                UpnpMinorVersion = "0",
                IsUpnp2 = true
            },
            IpEndPoint = new IPEndPoint(IPAddress.Parse("[Your IP Address]"), 1901),
            TypeName = "Root-Device",
            Version = 1,
            EntityType = EntityType.RootDevice,
            CONFIGID = "100",
            Services = new List<IServiceConfiguration>
            {
                new ServiceConfiguration
                {
                    TypeName = "Root-Service-1",
                    Version = 1,
                    EntityType = EntityType.ServiceType
                },
                new ServiceConfiguration
                {
                    TypeName = "Root-Service-2",
                    Domain = "Root-Service-Domain-1",
                    Version = 2,
                    EntityType = EntityType.DomainService
                },
            },
            EmbeddedDevices = new List<IDeviceConfiguration>
            {
                new DeviceConfiguration
                {
                    TypeName = "Embed-Device-1",
                    Version = 1,
                    EntityType = EntityType.Device,
                    DeviceUUID = Guid.NewGuid().ToString(),
                    Services = new List<IServiceConfiguration>
                    {
                        new ServiceConfiguration
                        {
                            TypeName = "Embed-Device-1-Service-1",
                            Version = 1,
                            EntityType = EntityType.ServiceType
                        },
                        new ServiceConfiguration
                        {
                            TypeName = "Embed-Device-1-Service-2",
                            Domain = "Embed-1-Service-2-Domain-2",
                            Version = 2,
                            EntityType = EntityType.DomainService
                        },
                    }
                },
                new DeviceConfiguration
                {
                    TypeName = "Embed-Device-2",
                    Version = 1,
                    EntityType = EntityType.DomainDevice,
                    Domain = "Embed-Device-2-Domain-2",
                    DeviceUUID = Guid.NewGuid().ToString(),
                    Services = new List<IServiceConfiguration>
                    {
                        new ServiceConfiguration
                        {
                            TypeName = "Embed-Device-2-Service-1",
                            Version = 1,
                            EntityType = EntityType.ServiceType,
                            },
                        new ServiceConfiguration
                        {
                            TypeName = "Embed-Device-2-Service-2",
                            Domain = "Embed-Service-Domain-2",
                            Version = 2,
                            EntityType = EntityType.DomainService
                        },
                    }
                }
            }

        };
    }
}
```
