# SSDP Library for UPnP version 2.0 

[![NuGet Badge](https://buildstats.info/nuget/SSDP.UPnP.PCL)](https://www.nuget.org/packages/SSDP.UPnP.PCL/)

[![.NET Standard](http://img.shields.io/badge/.NET_Standard-v2.0-red.svg)](https://docs.microsoft.com/da-dk/dotnet/articles/standard/library) 

[![System.Reactive](http://img.shields.io/badge/Rx-v4.0.0-ff69b4.svg)](http://reactivex.io/) 

[![UPnP](http://img.shields.io/badge/UPnP_Device_Architecture-v2.0-blue.svg)](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf)

*Please star this project if you find it useful. Thank you.*

## Why This Library
There are other SSDP Libraries available, so why this library?

This library support the v2.0 version of the UPnP Arhitecture. Most other libraries are for UPnP v1.1.

This library is created for [Reactive Extensions](http://reactivex.io/ "Reactive Extensions"). As SSDP deals with a stream of messages continuously coming in, Rx IMHO provides a much more elegant programming paradigm than what exists already. Sure the Rx learning curve can feel a bit steep at first, but it is worth the effort.

This library is created for .NET Standard 2.0 making it modern and ready for the future. Also, the library seeks to balance broad compatibility with simplicity by supporting only the most recent platforms - i.e. iOS, Android, UWP (16299+), .NET Core 1.0+ and .NET 4.6.1+ and Mono. So no support for older versions of Windows Phone or Silver Light for this library.

This project is based on [SocketLite.PCL](https://github.com/1iveowl/SocketLite.PCL) for cross platform TCP sockets support, that uses the "Bait and Switch" pattern. To read about "Bait and Switch" I can recoomend reading this great short blog post: [The Bait and Switch PCL Trick](http://log.paulbetts.org/the-bait-and-switch-pcl-trick/).

## Version 6.0
In this version large parts of this library was improved for much higher reliability and stability. This also introduced some breaking changes. It is strong encouraged to adapt this version over previous versions.

## Version 5.0
Moved from .NET Standard 1.3 to .NET Standard 2.0. IF you need to use this library in projects that does not support .NET Standard 2.0 then use an earlier version if this library.

Removed Obsolete methods from Library.

## Version 4.0
Version 4.0 represents a major overhaul of this library. Version 4.0 is still backwards compatible, but many of the methods have been marked as deprecated to inspire developers to use the newer versions of this library. In previous versions you had to subscribe to an observable and then start the action. In version 4.0 you just subscribe, that's it. Much more clean and better aligned with the Rx patterns.

There is still UWP support in version 4.0, but the emphasis has been on .NET Core and it will be going forward

## Getting Started With the Control Point
Using the ControlPoint provided in this library is easy. Still, to fully appreciate the SSDP protocol and how it should be used it is highly recommended to read about the details in the [UPnP 2.0 Specification](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf).

In the sample code we will start a listener that sends out a SSDP search request and listens for all SSDP search replies as well as any SSDP notifications that my be on the local network.

**IMPORTANT** If you are not seeing MSearch responses or Notify messages when using the following example and your are running Windows, then try and stop the Windows SSDP Service to prevent this service from intercepting these messages so that thet neven reach you clint code. 

```csharp
class Program
{
    private static IControlPoint _controlPoint;
    private static IPAddress _controlPointLocalIp;


   // For this test to work you most likely need to stop the SSDP Discovery service on Windows
    // If you don't stop the SSDP Windows Service, the service will intercept the UPnP multicasts and consequently nothing will show up in the console. 

    static async Task Main(string[] args)
    {
        _controlPointLocalIp = IPAddress.Parse("192.168.0.59");

        var cts = new CancellationTokenSource();

        await StartAsync(cts.Token);

        System.Console.WriteLine("Press any key to end (1).");

        System.Console.ReadKey();

        cts.Cancel();

        System.Console.WriteLine("Press any key to end (2)");
        System.Console.ReadKey();

    }

    private static async Task StartAsync(CancellationToken ct)
    {
        //StartDeviceListening();

        await StartControlPointListeningAsync(ct);
    }

    private static async Task StartControlPointListeningAsync(CancellationToken ct)
    {
        _controlPoint = new ControlPoint(_controlPointLocalIp);

        _controlPoint.Start(ct);

        await ListenToNotify(ct);

        await ListenToMSearchResponse(ct);
        
        await StartMSearchRequestMulticastAsync();
    }

    private static async Task ListenToNotify(CancellationToken ct)
    {
        var counter = 0;

        var observerNotify = _controlPoint.CreateNotifyObservable();

        var disposableNotify = observerNotify
            .Subscribe(
                n =>
                {
                    counter++;
                    System.Console.BackgroundColor = ConsoleColor.DarkBlue;
                    System.Console.ForegroundColor = ConsoleColor.White;
                    System.Console.WriteLine($"---### Control Point Received a NOTIFY - #{counter} ###---");
                    System.Console.ResetColor();
                    System.Console.WriteLine($"{n.NotifyCastMethod.ToString()}");
                    System.Console.WriteLine($"From: {n.Name}:{n.Port}");
                    System.Console.WriteLine($"Location: {n?.Location?.AbsoluteUri}");
                    System.Console.WriteLine($"Cache-Control: max-age = {n.CacheControl}");
                    System.Console.WriteLine($"Server: " +
                                             $"{n.Server.OperatingSystem}/{n.Server.OperatingSystemVersion} " +
                                             $"UPNP/" +
                                             $"{n.Server.UpnpMajorVersion}.{n.Server.UpnpMinorVersion}" +
                                             $" " +
                                             $"{n.Server.ProductName}/{n.Server.ProductVersion}" +
                                             $" - ({n.Server.FullString})");
                    System.Console.WriteLine($"NT: {n.NT}");
                    System.Console.WriteLine($"NTS: {n.NTS}");
                    System.Console.WriteLine($"USN: {n.USN}");
                    System.Console.WriteLine($"BOOTID: {n.BOOTID}");
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

                    if (n.HasParsingError)
                    {
                        System.Console.WriteLine($"Parsing errors: {n.HasParsingError}");
                    }

                    System.Console.WriteLine();
                });
    }

    private static async Task ListenToMSearchResponse(CancellationToken ct)
    {

        var mSearchResObs = _controlPoint.CreateMSearchResponseObservable();

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
                    System.Console.WriteLine($"{res?.ResponseCastMethod.ToString()}");
                    System.Console.WriteLine($"From: {res?.Name}:{res.Port}");
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
                    System.Console.WriteLine($"ST: {res.ST}");
                    System.Console.WriteLine($"USN: {res.USN}");
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
            SearchCastMethod = CastMethod.Multicast,
            CPFN = "MyTestSSDPControlPoint",
            Name = UdpSSDPMultiCastAddress,
            Port = UdpSSDPMulticastPort,
            MX = TimeSpan.FromSeconds(5),
            TCPPORT = TcpResponseListenerPort.ToString(),
            ST = new ST
            {
                StSearchType = STSearchType.All
            },
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

        // Send out SDDP Search request:
        await _controlPoint.SendMSearchAsync(mSearchMessage);
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

T
**IMPORTANT** Notice that you must create your own instance of the MSearch class and that it must implement the `IMSearchRequest` interface. It could be as simple as below or it could be more complex. You could even give the class a different name. It all depends on your needs. Only requirement is that it implements the interface `IMSearchRequest`:
```csharp
internal class MSearch : IMSearchRequest
{
    public bool InvalidRequest { get; } = false;
    public string HostIp { get; internal set; }
    public int HostPort { get; internal set; }
    public IDictionary<string, string> Headers { get; internal set; }
    public CastMethod SearchCastMethod { get; internal set; }
    public string MAN { get; internal set; }
    public TimeSpan MX { get; internal set; }
    public string ST { get; internal set; }
    public IUserAgent UserAgent { get; internal set; }
    public string CPFN { get; internal set; }
    public string CPUUID { get; internal set; }
    public string TCPPORT { get; internal set; }        
}

internal class UserAgent : IUserAgent
{
    public string FullString { get; internal set; }
    public string OperatingSystem { get; internal set; }
    public string OperatingSystemVersion { get; internal set; }
    public string ProductName { get; internal set; }
    public string ProductVersion { get; internal set; }
    public string UpnpMajorVersion { get; internal set; }
    public string UpnpMinorVersion { get; internal set; }
    public bool IsUpnp2 { get; internal set; }
}
```

## Device
Using the Device provided in this library is easy. Still, to fully appreciate the SSDP protocol and how it should be used it is highly recommended to read about the details in the [UPnP 2.0 Specification](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf).

Listening and responding to MSearch Requests from Control Points could look something like this:
```csharp
private static async Task StartDeviceListening()
{
    _device = new Device(_httpListener);

	// The allowMultipleBindingToPort option is useful on Windows, that by default does not allow multiple binding to a port
    var mSearchObservable = await _device.CreateMSearchObservable(allowMultipleBindingToPort:false);

    var subscription= mSearchObservable
        .Subscribe(
        async req =>
        {
            System.Console.BackgroundColor = ConsoleColor.DarkGreen;
            System.Console.ForegroundColor = ConsoleColor.White;
            System.Console.WriteLine($"---### Device Received a M-SEARCH REQUEST ###---");
            System.Console.ResetColor();
            System.Console.WriteLine($"{req.SearchCastMethod.ToString()}");
            System.Console.WriteLine($"HOST: {req.HostIp}:{req.HostPort}");
            System.Console.WriteLine($"MAN: {req.MAN}");
            System.Console.WriteLine($"MX: {req.MX.TotalSeconds}");
            System.Console.WriteLine($"USER-AGENT: " +
                                        $"{req.UserAgent?.OperatingSystem}/{req.UserAgent?.OperatingSystemVersion} " +
                                        $"UPNP/" +
                                        $"{req.UserAgent?.UpnpMajorVersion}.{req.UserAgent?.UpnpMinorVersion}" +
                                        $" " +
                                        $"{req.UserAgent?.ProductName}/{req.UserAgent?.ProductVersion}" +
                                        $" - ({req.UserAgent?.FullString})");

            System.Console.WriteLine($"CPFN: {req.CPFN}");
            System.Console.WriteLine($"CPUUID: {req.CPUUID}");
            System.Console.WriteLine($"TCPPORT: {req.TCPPORT}");

            if (req.Headers.Any())
            {
                System.Console.ForegroundColor = ConsoleColor.DarkYellow;
                System.Console.WriteLine($"Additional Headers: {req.Headers.Count}");
                foreach (var header in req.Headers)
                {
                    System.Console.WriteLine($"{header.Key}: {header.Value}; ");
                }
                System.Console.ResetColor();
            }

            System.Console.WriteLine();

            var mSearchResponse = new MSearchResponse
            {
                //HostIp = _hostIp,
                //HostPort = Initializer.UdpListenerPort,
                ResponseCastMethod = CastMethod.Unicast,
                StatusCode = 200,
                ResponseReason = "OK",
                CacheControl = TimeSpan.FromSeconds(30),
                Date = DateTime.Now,
                Ext = true,
                Location = new Uri($"http://{_remoteControlPointHost}:{req.TCPPORT}/test"),
                Server = new Server
                {
                    OperatingSystem = "Windows",
                    OperatingSystemVersion = "10.0",
                    IsUpnp2 = true,
                    ProductName = "Tester",
                    ProductVersion = "0.1",
                    UpnpMajorVersion = "2",
                    UpnpMinorVersion = "0"
                },
                ST = req.ST,
                USN = "uuid:device-UUID::upnp:rootdevice",
                BOOTID = "1"
            };

            await _device.SendMSearchResponseAsync(mSearchResponse, req);
        });
}
```

Sending Notify messages might looks like this:

```csharp
private static async Task StartSendingRandomNotify()
{
    var wait = new Random();
    var i = 0;

    while (true)
    {
        await Task.Delay(TimeSpan.FromSeconds(wait.Next(1,6)));
        i++;
        var newNotify = new Notify
        {
            BOOTID = i.ToString(),
            CacheControl = TimeSpan.FromSeconds(5),
            CONFIGID = "1",
            HostIp = _remoteControlPointHost,
            HostPort = 1900,
            Location = new Uri($"http://{_deviceLocalIp}:1900/Test"),
            NotifyCastMethod = CastMethod.Multicast,
            NT = "upnp:rootdevice",
            NTS = NTS.Alive,
            USN = "uuid:device-UUID::upnp:rootdevice",
            Server = new Server
            {
                OperatingSystem = "Windows",
                OperatingSystemVersion = "10.0",
                IsUpnp2 = true,
                ProductName = "Tester",
                ProductVersion = "0.1",
                UpnpMajorVersion = "2",
                UpnpMinorVersion = "0"
            },
        };

        await _device.SendNotifyAsync(newNotify);
    }
}
```
