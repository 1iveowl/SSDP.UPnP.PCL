using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Console.Device.NETCore.Model;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Service;
using SSDP.UPnP.PCL.Model;
using SSDP.UPnP.PCL.Service;


class Program
{
    private static IControlPoint _controlPoint;
    private static IDevice _device;

    private static IPAddress _deviceLocalIpAddress;
    private static IPAddress _remoteControlPointHost;

    private static IPEndPoint _localUnicastIpEndPoint;
    private static IPEndPoint _localMulticastIpEndPoint;

    // For this test to work you most likely need to stop the SSDP Discovery service on Windows
    // If you don't stop the SSDP Windows Service, the service will intercept the UPnP multicasts and consequently nothing will show up in the console. 

    static async Task Main(string[] args)
    {
        _localUnicastIpEndPoint = new IPEndPoint(IPAddress.Parse("192.168.0.59"), 8000);

        _deviceLocalIpAddress = IPAddress.Parse("192.168.0.59");
        _remoteControlPointHost = IPAddress.Parse("192.168.0.48");

        var cts = new CancellationTokenSource();

        await StartAsync(cts.Token);
      
        System.Console.ReadKey();
    }

    private static async Task StartAsync(CancellationToken ct)
    {
        StartDeviceListening();
        await StartSendingRandomNotify(ct);
    }

    private static async Task StartSendingRandomNotify(CancellationToken ct)
    {
        var wait = new Random();
        var i = 0;

        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(wait.Next(1,6)), ct);
            i++;
            var newNotify = new Notify
            {
                BOOTID = i.ToString(),
                CacheControl = TimeSpan.FromSeconds(5),
                CONFIGID = "1",
                Name = _remoteControlPointHost.ToString(),
                Port = 1900,
                Location = new Uri($"http://{_deviceLocalIpAddress}:1900/Test"),
                NotifyTransportType = TransportType.Multicast,
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

    private static void StartDeviceListening()
    {
        var rootDevice = new RootDeviceConfiguration
        {
            
        };

        _device = new Device(rootDevice)
        {
            //Location = new Uri($"http://{_remoteControlPointHost}/test"),
            //USNs = new List<IUSN>
            //{
            //    new USN
            //    {

            //    },
            //    new USN
            //    {

            //    },
            //},
            //Server = new Server
            //{
            //    OperatingSystem = "Windows",
            //    OperatingSystemVersion = "10.0",
            //    IsUpnp2 = true,
            //    ProductName = "Tester",
            //    ProductVersion = "0.1",
            //    UpnpMajorVersion = "2",
            //    UpnpMinorVersion = "0"
            //},


        };


        var cts = new CancellationTokenSource();

        _device.StartAsync(cts.Token);

        //var disposableMSearch= _device.MSearchRequestObservable()
        //    //.Where(req => req.Name == _remoteControlPointHost.ToString())
        //    .Do(req =>
        //    {
        //        System.Console.BackgroundColor = ConsoleColor.DarkGreen;
        //        System.Console.ForegroundColor = ConsoleColor.White;
        //        System.Console.WriteLine($"---### Device Received a M-SEARCH REQUEST ###---");
        //        System.Console.ResetColor();
        //        System.Console.WriteLine($"{req.SearchCastMethod.ToString()}");
        //        System.Console.WriteLine($"HOST: {req.Name}:{req.Port}");
        //        System.Console.WriteLine($"MAN: {req.MAN}");
        //        System.Console.WriteLine($"MX: {req.MX.TotalSeconds}");
        //        System.Console.WriteLine($"USER-AGENT: " +
        //                                 $"{req.UserAgent?.OperatingSystem}/{req.UserAgent?.OperatingSystemVersion} " +
        //                                 $"UPNP/" +
        //                                 $"{req.UserAgent?.UpnpMajorVersion}.{req.UserAgent?.UpnpMinorVersion}" +
        //                                 $" " +
        //                                 $"{req.UserAgent?.ProductName}/{req.UserAgent?.ProductVersion}" +
        //                                 $" - ({req.UserAgent?.FullString})");

        //        System.Console.WriteLine($"CPFN: {req.CPFN}");
        //        System.Console.WriteLine($"CPUUID: {req.CPUUID}");
        //        System.Console.WriteLine($"TCPPORT: {req.TCPPORT}");

        //        if (req.Headers.Any())
        //        {
        //            System.Console.ForegroundColor = ConsoleColor.DarkYellow;
        //            System.Console.WriteLine($"Additional Headers: {req.Headers.Count}");
        //            foreach (var header in req.Headers)
        //            {
        //                System.Console.WriteLine($"{header.Key}: {header.Value}; ");
        //            }
        //            System.Console.ResetColor();
        //        }

        //        System.Console.WriteLine();
        //    })
        //    .Select(req => new MSearchResponse
        //    {
        //        ResponseCastMethod = CastMethod.Unicast,
        //        StatusCode = 200,
        //        ResponseReason = "OK",
        //        CacheControl = TimeSpan.FromSeconds(30),
        //        Date = DateTime.Now,
        //        Ext = true,
        //        Location = new Uri($"http://{_remoteControlPointHost}:{req.TCPPORT}/test"),
        //        Server = new Server
        //        {
        //            OperatingSystem = "Windows",
        //            OperatingSystemVersion = "10.0",
        //            IsUpnp2 = true,
        //            ProductName = "Tester",
        //            ProductVersion = "0.1",
        //            UpnpMajorVersion = "2",
        //            UpnpMinorVersion = "0"
        //        },
        //        ST = req.ST,
        //        USN = "uuid:device-UUID::upnp:rootdevice",
        //        BOOTID = "1",
        //        RemoteHost = new Host(req),
        //        MX = req.MX
        //    })
        //    .Select(res => Observable.FromAsync(() => _device.SendMSearchResponseAsync(res)))
        //    .Concat()
        //    .Subscribe(
        //        _ =>
        //        {
        //            System.Console.WriteLine("M-Search Response send.");
        //        },
        //        ex =>
        //        {
        //            System.Console.WriteLine(ex);
        //        },
        //        () =>
        //        {
        //            System.Console.WriteLine("Completed.");
        //        });
    }
}