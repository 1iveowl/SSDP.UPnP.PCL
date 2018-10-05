# SSDP Library for UPnP version 2.0 

[![NuGet Badge](https://buildstats.info/nuget/SSDP.UPnP.PCL)](https://www.nuget.org/packages/SSDP.UPnP.PCL/)

[![.NET Standard](http://img.shields.io/badge/.NET_Standard-v2.0-red.svg)](https://docs.microsoft.com/da-dk/dotnet/articles/standard/library) 

[![System.Reactive](http://img.shields.io/badge/Rx-v4.1.1-ff69b4.svg)](http://reactivex.io/) 

[![UPnP](http://img.shields.io/badge/UPnP_Device_Architecture-v2.0-blue.svg)](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf)

*Please star this project if you find it useful. Thank you.*

## Why This Library
There are other SSDP Libraries available, so why this library?

This library support the v2.0 version of the UPnP Arhitecture. Most other libraries are for UPnP v1.1.

This library is created for [Reactive Extensions](http://reactivex.io/ "Reactive Extensions"). As SSDP deals with a stream of messages continuously coming in, Rx IMHO provides a much more elegant programming paradigm than what exists already. Sure the Rx learning curve can feel a bit steep at first, but it is worth the effort.

This library is created for .NET Standard 2.0 making it modern and ready for the future. Also, the library seeks to balance broad compatibility with simplicity by supporting only the most recent platforms - i.e. iOS, Android, UWP (16299+), .NET Core 1.0+ and .NET 4.6.1+ and Mono. So no support for older versions of Windows Phone or Silver Light for this library.

## Version 6.0
In this version large parts of this library was improved for much higher reliability and stability. This also introduced some breaking changes. It is strong encouraged to adapt this version over previous versions.

The ControlPoint is much more reliable and us considered stabile.

The Device is also great improved, but it is adviced that Device is used with care as more testing is needed. 

Both ControlPoint and Device is now multi-homed capable.  

## Getting Started With the Control Point
Using the ControlPoint is easy. In the sample code we will start a listener that sends out a SSDP search request and listens for all SSDP search replies as well as any SSDP notifications that my be on the local network.

**IMPORTANT** If you are not seeing MSearch responses or Notify messages when using the following example and your are running Windows, then try and stop the Windows SSDP Service to prevent this service from intercepting these messages so that thet neven reach you clint code. Even when disabling SSDP there is a risk that some other SSDP listener is running on your PC, and thus intercepting the UDP messages before reaching this library.

### ControlPoint Construction

There are two constructors available for instanciating a ControlPoint. 

1. IP Address constructor
2. Interface constructor (advanced)

Using 1. all you have to do is to specify one or more IP Addresses on which you want to create the ControlPoint. Usually you will only specify one IP Address, which is the IP Address of the network interface where you want your ControlPoint to run on. If you specify more than one IP Address the ControlPoint will run as a Multi-Homed ControlPoint.

Using 2. you will have intiate one or more ControlPoint Interfaces yourself and use these in the contructure. Each ControlPoint interface must implement the interface `IControlPointInterface`. Again, if you specify more than one ControlPoint Interface, then the ControlPoint will run as a Multi-Homed ControlPoint.

In the example below the the first option, IP Address contructor, is used.

### Starting the ControlPoint
When you start the ControlPoint, you have two options, as you can either call the Method:

1. `StartAsync`
2. or `HotStartAsync` (advanced)

When you use option 1. `Start`, then the ControlPoint will set up it's listeners, based on the parameters you provided as part of contruction.

When using option 2. `HotStart`, then you need to provide an observable with the this signature: `IObservable<IHttpRequestResponse>`. This option is more advanced, but could play in to more complex scenarios where you want to share the Observable across other services, like for instance UPnP Eventing. 

In the example below the first option, `Start`, is used.

### ControlPoint Example

In the following example a ControlPoint using the IP Address you specify is created. The ControlPoint is set up to listen for M-SEARCH Responses as well as NOTIFY broad-casts. In the example both message types are being listed as output in a Console App. In a real scenario you would set up some logic to handled such incoming messages.

Finally, in the example, a M-SEARCH SSDP Discovery is broad-casted. The effect of this broad-cast is that all UPnP clients on the local network should responed with M-SEARCH responses and because we are listening for those, the responses will appear as output in our Console test app.


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

### Start Search for UPnP Devices
More details about searching for devices is specified in the UPnP Specification. Regarding the ST paramater the [UPnP v2.0 specification](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf) states:

ST 
Required. Field value contains Search Target. shall be one of the following. (See NT header field in NOTIFY with ssdp:alive above.) Single URI. 
* `ssdp:all` Search for all devices and services. 
* `upnp:rootdevice` Search for root devices only. 
* `uuid:device-UUID` Search for a particular device. device-UUID specified by UPnP vendor. See clause 1.1.4, UUID format and recommended generation algorithms for the MANDATORY UUID format. 
* `urn:schemas-upnp-org:device:deviceType:ver` Search for any device of this type where deviceType and ver are defined by the UPnP Forum working committee. 
* `urn:schemas-upnp-org:service:serviceType:ver` Search for any service of this type where serviceType and ver are defined by the UPnP Forum working committee. 
* `urn:domain-name:device:deviceType:ver` Search for any device of this typewhere domain-name (a Vendor Domain Name), deviceType and ver are defined by the UPnP vendor and ver specifies the highest specifies the highest supported version of the device type. Period characters in the Vendor Domain Name shall be replaced with hyphens in accordance with RFC 2141. 
* `urn:domain-name:service:serviceType:ver` Search for any service of this type. Where domain-name (a Vendor Domain Name), serviceType and ver are defined by the UPnP vendor and ver specifies the highest specifies the highest supported version of the service type. Period characters in the Vendor Domain Name shall be replaced with hyphens in accordance with RFC 2141. 

**IMPORTANT** Notice that you must create your own instance of the MSearch class and that it must implement the `IMSearchRequest` interface. It could be as simple as below or it could be more complex. You could even give the class a different name. It all depends on your needs. Only requirement is that it implements the interface `IMSearchRequest`:
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
The implementation if the UPnP 2.0 Device is still work in progress. Use with care and feel free to provide input, bugs and improvements here.  

### Device Construction

There are two ways to create a Device.

1. Root Device Configuration
2. Root Device Interface (advanced)

To create a Device using 1. Root Device Configuration you need to create an instance of a class that implements the interface `IRootDeviceConfiguration`. Such a class is provided as part of the library. 

To create a Device using 2. Root Device Interface, you need to create an instance of one or more classes that implements the interface `IRootDeviceInterface`. The difference from the first option is that you can create multiple interfaces, effectively making the device Multi-Homed. Also, you need to specify the UDP Clients youself. This is the more advances option of the two, but could come in handy, if sharing UDP client's with for instance a ControlPoint of UPnP Eventing etc. 

In the example below option 1. Root Device Configuration, is used. 

### Device Start

Similar to the ControlPoint, a Device can be started with two methods:

1. `Start`
2. or `HotStart` (advanced)

When you use option 1. `Start`, then the Device will set up it's listeners, based on the parameters you provided as part of the contructor.

When using option 2. `HotStart`, then you need to provide an observable with the this signature: `IObservable<IHttpRequestResponse>`. This option is more advanced, but could play in to more complex scenarios where you want to share the Observable across other services, like for instance UPnP Eventing. 

In the example below the first option, `Start`, is used.



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
