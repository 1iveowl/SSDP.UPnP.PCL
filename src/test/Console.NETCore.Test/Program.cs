using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Console.NETCore.Test.Model;
using ISSDP.UPnP.PCL.Enum;
using ISSDP.UPnP.PCL.Interfaces.Service;
using SSDP.UPnP.PCL.ExtensionMethod;
using SSDP.UPnP.PCL.Model;
using SSDP.UPnP.PCL.Service;
using static SSDP.UPnP.PCL.Helper.Constants;

class Program
{
    private static IControlPoint _controlPoint;
    private static IPAddress _controlPointLocalIp1 = null;
    //private static IPAddress _controlPointLocalIp2;

    //private static IPAddress _deviceRemoteIp1;


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

        //_controlPointLocalIp2 = IPAddress.Parse("169.254.38.70");

        //_deviceRemoteIp1 = IPAddress.Parse("192.168.0.48");

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
        _controlPoint = new ControlPoint(_controlPointLocalIp1);

        _controlPoint.Start(ct);

        ListenToMSearchResponse(ct);

        ListenToNotify();

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

        //await Task.Delay(TimeSpan.FromSeconds(1));

        //await _controlPoint.SendMSearchAsync(mSearchMessage);

        //await Task.Delay(TimeSpan.FromSeconds(1));
        //await _controlPoint.SendMSearchAsync(mSearchMessage);
        //await Task.Delay(TimeSpan.FromSeconds(1));

        //await _controlPoint.SendMSearchAsync(mSearchMessage);
    }
}