using System.Net;
using System.Reactive.Linq;
using SSDP.UPnP.PCL;
using SSDP.UPnP.PCL.Model;

// SSDP control point sample: listens for M-SEARCH responses and NOTIFY
// advertisements, and multicasts an ssdp:all discovery request.
//
// On Windows, stop the "SSDP Discovery" service first — it intercepts the UPnP
// multicasts, and nothing will show up in the console while it runs.

var ipAddress = args.Length > 0 && IPAddress.TryParse(args[0], out var parsed)
    ? parsed
    : Constants.GetBestGuessLocalIPAddress();

if (ipAddress is null)
{
    Console.WriteLine("No suitable local IPv4 address found. Pass one as the first argument.");
    return;
}

Console.WriteLine($"IP Address: {ipAddress}");

using var cts = new CancellationTokenSource();
using var controlPoint = new ControlPoint(ipAddress);

controlPoint.Start(cts.Token);

using var notifySubscription = controlPoint.NotifyObservable()
    .Subscribe(notify =>
    {
        Console.WriteLine($"---### NOTIFY {notify.NTS} ###---");
        Console.WriteLine($"From: {notify.RemoteIpEndPoint} NT: {notify.NT}");
        Console.WriteLine($"USN: {notify.USN?.USNString} Location: {notify.Location}");
        Console.WriteLine();
    });

using var responseSubscription = controlPoint.MSearchResponseObservable()
    .Subscribe(response =>
    {
        Console.WriteLine($"---### M-SEARCH RESPONSE ({response.TransportType}) ###---");
        Console.WriteLine($"From: {response.RemoteIpEndPoint} Status: {response.StatusCode} {response.ResponseReason}");
        Console.WriteLine($"ST: {response.ST?.STString} USN: {response.USN?.USNString}");
        Console.WriteLine($"Server: {response.Server.FullString} Location: {response.Location}");
        Console.WriteLine();
    });

await controlPoint.SendMSearchAsync(
    new MSearchRequest
    {
        TransportType = TransportType.Multicast,
        MX = TimeSpan.FromSeconds(5),
        ST = new ST { StSearchType = STType.All },
        CPFN = "SSDP.UPnP.PCL Sample Control Point",
        UserAgent = new UserAgent
        {
            OperatingSystem = Environment.OSVersion.Platform.ToString(),
            OperatingSystemVersion = Environment.OSVersion.Version.ToString(2),
            ProductName = "SSDP.UPNP.PCL",
            ProductVersion = "7.0"
        }
    },
    ipAddress);

Console.WriteLine("Listening. Press any key to stop.");
Console.ReadKey();

cts.Cancel();
