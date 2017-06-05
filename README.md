# SSDP Library for UPnP version 2.0 

[![NuGet Badge](https://buildstats.info/nuget/SSDP.UPnP.PCL)](https://github.com/1iveowl/SSDP.UPnP.PCL)

[![.NET Standard](http://img.shields.io/badge/.NET_Standard-v1.2-green.svg)](https://docs.microsoft.com/da-dk/dotnet/articles/standard/library) 

[![System.Reactive](http://img.shields.io/badge/Rx-v3.1.1-ff69b4.svg)](http://reactivex.io/) 

[![UPnP](http://img.shields.io/badge/UPnP_Device_Architecture-v2.0-blue.svg)](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf)

*Please star this project if you find it useful. Thank you.*

## Why This Library
There are other SSDP Libraries available, so why this library?

This library support the v2.0 version of the UPnP Arhitecture. Most other libraries are only for UPnP v1.1.

This library is created for [Reactive Extensions](http://reactivex.io/ "Reactive Extensions"). As SSDP deals with a stream of messages continuously coming in, Rx IMHO provides a much more elegant programming paradigm than what exists already. Sure the Rx learning curve can feel a bit steep at first, but it is worth the effort.

This library is created for .NET Standard 1.2 making it modern and ready for the future. Also, the library seeks to balance broad compatibility with simplicity by supporting only the most recent platforms - i.e. iOS, Android, UWP, .NET Core 1.0+ and .NET 4.5.1+ and Mono. So no support for older versions of Windows Phone or Silver Light for this library.

This project is based on [SocketLite.PCL](https://github.com/1iveowl/SocketLite.PCL) for cross platform TCP sockets support, that uses the "Bait and Switch" pattern. To read about "Bait and Switch" I can recoomend reading this great short blog post: [The Bait and Switch PCL Trick](http://log.paulbetts.org/the-bait-and-switch-pcl-trick/).


## Version 4.0
Version 4.0 represents a major overhaul of this library. Version 4.0 is still backwards compatible, but many of the methods have been marked as deprecated to inspire developers to use the newer versions of this library. In previous versions you had to subscribe to an observable and then start the action. In version 4.0 you just subscribe, that's it. Much more clean and better aligned with the Rx patterns.

There us still UWP support in version 4.0, but the emphasis has been on .NET Core and it will be going forward

## Getting Started With Control Point and Devices Easy


### Using Statements
Using Statements assumed in the following code examples:
```csharp
using ISimpleHttpServer.Service;
using SimpleHttpServer.Service;

using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Service;

using SSDP.Console.Test.NET.Model;
using SSDP.UPnP.PCL.Service;
```

Start with getting a HttpListener that is configuered for SSDP:
```csharp
httpListener = await Initializer.GetHttpListener("192.168.0.2");
```
Then create a SSDP Control Point or a Device Control using the just created listener:
```csharp
var controlPoint = new new ControlPoint(httpListener);
// or
var device = new Device(httpListener);
```
## Control Point

### Listening For Notify Message
This can be done like this:
```csharp
private static async Task ListenToNotify()
{
    var counter = 0;

    var observerNotify = await _controlPoint.CreateNotifyObservable();

    var subscription = observerNotify
        .Subscribe(
            n =>
            {
                // Example code
                counter++;
                System.Console.BackgroundColor = ConsoleColor.DarkBlue;
                System.Console.ForegroundColor = ConsoleColor.White;
                System.Console.WriteLine($"---### Control Point Received a NOTIFY - #{counter} ###---");
                System.Console.ResetColor();
                System.Console.WriteLine($"{n.NotifyCastMethod.ToString()}");
                System.Console.WriteLine($"From: {n.HostIp}:{n.HostPort}");
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

                System.Console.WriteLine();
            },
            ex => 
            {
                // Insert code to deal with exceptions here.
            },
            () => 
            {
                // Insert code dealing with completion here.
            });
}
```

### Start Search for UPnP Devices
To search for devices like this:
```csharp

private static async Task StartMSearchRequestMulticastAsync()
{
    // Create a search package
    var mSearchMessage = new MSearch
    {
        SearchCastMethod = CastMethod.Multicast,
        CPFN = "TestXamarin",
        HostIp = "239.255.255.250",
        HostPort = 1900,
        MX = TimeSpan.FromSeconds(5),
        TCPPORT = Initializer.TcpResponseListenerPort.ToString(),
        ST = new ST
            {
                STtype  = STtype.RootDevice
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

    // Send out a search
    await _controlPoint.SendMSearchAsync(mSearchMessage);
}
```

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

internal class ST : IST
{
    public STtype STtype { get; internal set; }
    public string DeviceUUID { get; internal set; }
    public string Type { get; internal set; }
    public string Version { get; internal set; }
    public string DomainName { get; internal set; }
    public bool HasDomain { get; internal set; }
}

```

### Listen to MSearch Reponses
To listen to MSearch responses

```csharp
private static async Task ListenToMSearchResponse()
{

    var mSeachResObs = await _controlPoint.CreateMSearchResponseObservable(Initializer.TcpResponseListenerPort);

    var counter = 0;

    var mSearchresponseSubscribe = mSeachResObs
        //.Where(req => req.HostIp == _remoteDeviceIp)
        //.SubscribeOn(Scheduler.CurrentThread)
        .Subscribe(
            res =>
            {
                // Example code
                counter++;
                System.Console.BackgroundColor = ConsoleColor.DarkBlue;
                System.Console.ForegroundColor = ConsoleColor.White;
                System.Console.WriteLine($"---### Control Point Received a  M-SEARCH RESPONSE #{counter} ###---");
                System.Console.ResetColor();
                System.Console.WriteLine($"{res.ResponseCastMethod.ToString()}");
                System.Console.WriteLine($"From: {res.HostIp}:{res.HostPort}");
                System.Console.WriteLine($"Status code: {res.StatusCode} {res.ResponseReason}");
                System.Console.WriteLine($"Location: {res.Location.AbsoluteUri}");
                System.Console.WriteLine($"Date: {res.Date.ToString(CultureInfo.CurrentCulture)}");
                System.Console.WriteLine($"Cache-Control: max-age = {res.CacheControl}");
                System.Console.WriteLine($"Server: " +
                                            $"{res.Server.OperatingSystem}/{res.Server.OperatingSystemVersion} " +
                                            $"UPNP/" +
                                            $"{res.Server.UpnpMajorVersion}.{res.Server.UpnpMinorVersion}" +
                                            $" " +
                                            $"{res.Server.ProductName}/{res.Server.ProductVersion}" +
                                            $" - ({res.Server.FullString})");
                System.Console.WriteLine($"ST: {res.ST}");
                System.Console.WriteLine($"USN: {res.USN}");
                System.Console.WriteLine($"BOOTID.UPNP.ORG: {res.BOOTID}");
                System.Console.WriteLine($"CONFIGID.UPNP.ORG: {res.CONFIGID}");
                System.Console.WriteLine($"SEARCHPORT.UPNP.ORG: {res.SEARCHPORT}");
                System.Console.WriteLine($"SECURELOCATION: {res.SECURELOCATION}");

                if (res.Headers.Any())
                {
                    System.Console.ForegroundColor = ConsoleColor.DarkYellow;
                    System.Console.WriteLine($"Additional Headers: {res.Headers.Count}");
                    foreach (var header in res.Headers)
                    {
                        System.Console.WriteLine($"{header.Key}: {header.Value}; ");
                    }
                    System.Console.ResetColor();
                }

                System.Console.WriteLine();
            },
            ex => 
            {
                // Insert code to deal with exceptions here.
            },
            () => 
            {
                // Insert code dealing with completion here.
            });

    await StartMSearchRequestMulticastAsync();
}
```
### 

**IMPORTANT** If you are not seeing MSearcg responses or Notify messages try and your are running Windows, the try and stop the SSDP service so that it does not intercept the messages before they reach your code. 

For details about what a multicast M-SEARCH Request is and how to use it: see the [UPnP Architecture documentation](http://upnp.org/specs/arch/UPnP-arch-DeviceArchitecture-v2.0.pdf)). 

## Device

Listening and responding to MSearch Requests from Control Points could look something like this:
```csharp
private static async Task StartDeviceListening()
{
    _device = new Device(_httpListener);

    var mSearchObservable = await _device.CreateMSearchObservable();

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
