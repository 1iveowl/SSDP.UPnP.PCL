using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Console.NETCore.Test.Model;

using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Service;
using SSDP.UPnP.PCL.Service;
using static SSDP.UPnP.PCL.Helper.Constants;

class Program
{
    private static IControlPoint _controlPoint;
    private static IDevice _device;
    private static IPAddress _controlPointLocalIp;


    //private static string _remoteDeviceIp = "10.10.2.170";


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
        
        //while (true)
        //{
        //    await Task.Delay(TimeSpan.FromSeconds(10));
        //}
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

        // Use allowMultipleBindingToPort:true on Windows
        var observerNotify = await _controlPoint.CreateNotifyObservable();

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

                    if (n.ParsingErrors > 0)
                    {
                        System.Console.WriteLine($"Parsing errors: {n.ParsingErrors}");
                    }

                    System.Console.WriteLine();
                });
    }

    private static async Task ListenToMSearchResponse(CancellationToken ct)
    {

        var mSeachResObs = await _controlPoint.CreateMSearchResponseObservable();

        var counter = 0;

        var mSearchresponseSubscribe = mSeachResObs
            //.Where(req => req.HostIp == _remoteDeviceIp)
            //.SubscribeOn(Scheduler.CurrentThread)
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
                    System.Console.WriteLine($"ST: {res?.ST}");
                    System.Console.WriteLine($"USN: {res?.USN}");
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

                    if (res.ParsingErrors > 0)
                    {
                        System.Console.WriteLine($"Parsing errors: {res.ParsingErrors}");
                    }

                    System.Console.WriteLine();
                });
    }

    
    private static async Task StartMSearchRequestMulticastAsync()
    {
        var mSearchMessage = new MSearch
        {
            SearchCastMethod = CastMethod.Multicast,
            CPFN = "TestXamarin",
            
            Name = UdpSSDPMultiCastAddress,
            Port = UdpSSDPMulticastPort,
            MX = TimeSpan.FromSeconds(5),
            TCPPORT = TcpResponseListenerPort.ToString(),
            ST = new ST
            {
                STtype = STtype.All
            },
            //ST = new ST
            //{
            //    STtype  = STtype.ServiceType,
            //    Type = "SwitchPower",
            //    Version = "1",
            //    HasDomain = false
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

        await _controlPoint.SendMSearchAsync(mSearchMessage);

        await Task.Delay(TimeSpan.FromSeconds(1));

        await _controlPoint.SendMSearchAsync(mSearchMessage);

        await Task.Delay(TimeSpan.FromSeconds(1));
        await _controlPoint.SendMSearchAsync(mSearchMessage);
        await Task.Delay(TimeSpan.FromSeconds(1));

        await _controlPoint.SendMSearchAsync(mSearchMessage);
    }
}