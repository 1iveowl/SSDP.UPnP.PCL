using BenchmarkDotNet.Attributes;
using SSDP.UPnP.PCL.Model;

namespace SSDP.UPnP.PCL.Benchmarks;

/// <summary>
/// The receive path: what a control point pays per message from the network.
/// Allocation is the number that matters here, not nanoseconds - the library is
/// network-bound, and the claim being tested is about garbage, not throughput.
/// </summary>
[MemoryDiagnoser]
public class ParseBenchmarks
{
    private const string ServiceUsn = "uuid:5e9f3b1a-1f0c-4d4a-9a2d-1f6a3c9a0e11::urn:schemas-upnp-org:service:ContentDirectory:1";
    private const string RootDeviceUsn = "uuid:5e9f3b1a-1f0c-4d4a-9a2d-1f6a3c9a0e11::upnp:rootdevice";
    private const string DeviceSt = "urn:schemas-upnp-org:device:MediaServer:4";
    private const string ServerHeader = "Linux/6.12 UPnP/2.0 SSDP.UPNP.PCL/10.0";

    [Benchmark]
    public USN ParseServiceUsn() => USN.Parse(ServiceUsn).Value!;

    [Benchmark]
    public USN ParseRootDeviceUsn() => USN.Parse(RootDeviceUsn).Value!;

    [Benchmark]
    public ST ParseDeviceTypeSt() => ST.Parse(DeviceSt).Value!;

    [Benchmark]
    public ST ParseAllSt() => ST.Parse("ssdp:all").Value!;

    [Benchmark]
    public Server ParseServerHeader() => Parsing.SsdpMessageParser.ParseDeviceInfo<Server>(ServerHeader);
}
