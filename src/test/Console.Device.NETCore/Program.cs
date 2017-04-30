using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Console.Device.NETCore.Model;
using ISimpleHttpServer.Service;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Service;
using SSDP.UPnP.Netstandard.Helper;
using SSDP.UPnP.PCL.Service;

class Program
{
    private static IHttpListener _httpListener;

    private static IControlPoint _controlPoint;
    private static IDevice _device;

    private static string _deviceLocalIp = "10.10.2.170";
    private static string _remoteControlPointHost = "10.10.13.204";


    // For this test to work you most likely need to stop the SSDP Discovery service on Windows
    // If you don't stop the SSDP Windows Service, the service will intercept the UPnP multicasts and consequently nothing will show up in the console. 

    static void Main(string[] args)
    {
        StartAsync();
      
        System.Console.ReadKey();
    }

    private static async void StartAsync()
    {
        var ipv6MulticastAddressList = new List<string>
        {
            "ff02::c",
        };

        _httpListener = await Initializer.GetHttpListener(
            _deviceLocalIp, 
            Initializer.ListenerType.ControlPoint,
            ipv6MulticastAddressList);

        StartDeviceListening();
        await StartSendingRandomNotify();
    }

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
                Location = new Uri($"http://{_deviceLocalIp}:1901/Test"),
                NotifyCastMethod = CastMethod.Multicast,
                NT = "upnp:rootdevice",
                NTS = NTS.Alive,
                USN = "uuid:device-UUID:;upnp:rootdevice",
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

            await _device.Notify(newNotify);
        }
    }

    private static void StartDeviceListening()
    {
        _device = new Device(_httpListener);
        var MSearchRequestSubscribe = _device
            .MSearchObservable
            .Where(req => req.HostIp == _remoteControlPointHost)
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
                await _device.MSearchResponse(mSearchResponse, req);
            });
    }
}