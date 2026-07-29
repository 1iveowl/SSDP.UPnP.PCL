using System.Net;
using BenchmarkDotNet.Attributes;
using SSDP.UPnP.PCL.Model;
using SSDP.UPnP.PCL.Parsing;

namespace SSDP.UPnP.PCL.Benchmarks;

/// <summary>
/// The send path: what a device pays per advertisement and per answered search.
/// </summary>
[MemoryDiagnoser]
public class ComposeBenchmarks
{
    private static readonly Server Server = new()
    {
        OperatingSystem = "Linux",
        OperatingSystemVersion = "6.12",
        UpnpMajorVersion = 2,
        UpnpMinorVersion = 0,
        ProductName = "SSDP.UPNP.PCL",
        ProductVersion = "10.0"
    };

    private static readonly USN Usn = new()
    {
        EntityType = EntityType.RootDevice,
        DeviceUUID = "5e9f3b1a-1f0c-4d4a-9a2d-1f6a3c9a0e11"
    };

    private static readonly ST St = new()
    {
        StSearchType = STType.RootDeviceSearch,
        EntityType = EntityType.RootDevice
    };

    private static readonly MulticastMSearch Search = new()
    {
        MX = new MxSeconds(3),
        ST = new ST { StSearchType = STType.All },
        CPFN = "Benchmark Control Point",
        TCPPORT = new DynamicPort(51900)
    };

    private static readonly MSearchResponse Response = new()
    {
        MaxAge = TimeSpan.FromSeconds(1800),
        Date = new DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.Zero),
        Location = new Uri("http://192.168.0.10/description.xml"),
        Server = Server,
        ST = St,
        USN = Usn,
        BOOTID = 1721600000,
        CONFIGID = 77,
        SEARCHPORT = new DynamicPort(49152),
        RemoteIpEndPoint = new IPEndPoint(IPAddress.Loopback, 41000)
    };

    private static readonly Notify Alive = new()
    {
        HOST = Constants.SsdpMulticastHost,
        MaxAge = TimeSpan.FromSeconds(1800),
        Location = new Uri("http://192.168.0.10/description.xml"),
        NT = "upnp:rootdevice",
        NTS = NTS.Alive,
        Server = Server,
        USN = Usn,
        BOOTID = 7,
        CONFIGID = 1
    };

    [Benchmark]
    public byte[] ComposeMulticastSearch() => DatagramComposer.ComposeMSearchRequest(Search);

    [Benchmark]
    public byte[] ComposeResponse() => DatagramComposer.ComposeMSearchResponse(Response);

    [Benchmark]
    public byte[] ComposeAliveNotify() => DatagramComposer.ComposeNotify(Alive);
}
