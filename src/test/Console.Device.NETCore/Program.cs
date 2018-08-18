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

    private static string _deviceLocalIp = "192.168.0.59";
    private static string _remoteControlPointHost = "192.168.0.23";


    // For this test to work you most likely need to stop the SSDP Discovery service on Windows
    // If you don't stop the SSDP Windows Service, the service will intercept the UPnP multicasts and consequently nothing will show up in the console. 

    static void Main(string[] args)
    {
        StartAsync();
      
        System.Console.ReadKey();
    }

    private static async void StartAsync()
    {
        _httpListener = await Initializer.GetHttpListener(_deviceLocalIp);

        await StartDeviceListening(_httpListener);
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
                Name = _remoteControlPointHost,
                Port = 1900,
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

    private static async Task StartDeviceListening(IHttpListener listener)
    {
        _device = new Device(listener);
        var mSearchObservable = await _device.CreateMSearchObservable(allowMultipleBindingToPort:true);

        var diposableMSearch= mSearchObservable
            .Where(req => req.Name == _remoteControlPointHost)
            .Subscribe(
            async req =>
            {
                System.Console.BackgroundColor = ConsoleColor.DarkGreen;
                System.Console.ForegroundColor = ConsoleColor.White;
                System.Console.WriteLine($"---### Device Received a M-SEARCH REQUEST ###---");
                System.Console.ResetColor();
                System.Console.WriteLine($"{req.SearchCastMethod.ToString()}");
                System.Console.WriteLine($"HOST: {req.Name}:{req.Port}");
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
}