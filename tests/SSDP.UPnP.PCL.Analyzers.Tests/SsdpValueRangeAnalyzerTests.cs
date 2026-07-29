using Xunit;

namespace SSDP.UPnP.PCL.Analyzers.Tests;

/// <summary>
/// Spans come from <c>{|SSDPnnn:…|}</c> markup rather than hand-counted line and
/// column numbers, which are wrong as often as they are unreadable.
/// </summary>
public class SsdpValueRangeAnalyzerTests
{
    private const string Usings = """
        using System;
        using System.Net;
        using SSDP.UPnP.PCL.Model;

        """;

    // ---------------------------------------------------------------- SSDP001

    [Theory]
    [InlineData(6)]
    [InlineData(30)]
    [InlineData(120)]
    public async Task Mx_AboveRecommendedMaximum_IsReported(int seconds) =>
        await Verify.AnalyzerAsync(Usings + $$"""
            class C
            {
                MulticastMSearch M() => new MulticastMSearch
                {
                    ST = new ST { StSearchType = STType.All },
                    CPFN = "CP",
                    MX = {|SSDP001:new MxSeconds({{seconds}})|}
                };
            }
            """);

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task Mx_WithinRange_IsNotReported(int seconds) =>
        await Verify.AnalyzerAsync(Usings + $$"""
            class C
            {
                MulticastMSearch M() => new MulticastMSearch
                {
                    ST = new ST { StSearchType = STType.All },
                    CPFN = "CP",
                    MX = new MxSeconds({{seconds}})
                };
            }
            """);

    [Fact]
    public async Task Mx_FromANonConstant_IsNotReported() =>
        await Verify.AnalyzerAsync(Usings + """
            class C
            {
                MxSeconds M(int seconds) => new MxSeconds(seconds);
            }
            """);

    [Fact]
    public async Task Mx_FromAConstant_IsReported() =>
        await Verify.AnalyzerAsync(Usings + """
            class C
            {
                const int Wait = 9;
                MxSeconds M() => {|SSDP001:new MxSeconds(Wait)|};
            }
            """);

    [Fact]
    public async Task Mx_CodeFix_ClampsToTheRecommendedMaximum() =>
        await Verify.CodeFixAsync(
            Usings + """
            class C
            {
                MxSeconds M() => {|SSDP001:new MxSeconds(30)|};
            }
            """,
            Usings + """
            class C
            {
                MxSeconds M() => new MxSeconds(5);
            }
            """);

    // ---------------------------------------------------------------- SSDP003

    [Theory]
    [InlineData(80)]
    [InlineData(1900)]
    [InlineData(49151)]
    [InlineData(0)]
    public async Task DynamicPort_OutsideTheRange_IsReported(int port) =>
        await Verify.AnalyzerAsync(Usings + $$"""
            class C
            {
                DynamicPort M() => {|SSDP003:new DynamicPort({{port}})|};
            }
            """);

    [Theory]
    [InlineData(49152)]
    [InlineData(51900)]
    [InlineData(65535)]
    public async Task DynamicPort_InsideTheRange_IsNotReported(int port) =>
        await Verify.AnalyzerAsync(Usings + $$"""
            class C
            {
                DynamicPort M() => new DynamicPort({{port}});
            }
            """);

    [Fact]
    public async Task DynamicPort_AsTcpPortOnASearch_IsReported() =>
        await Verify.AnalyzerAsync(Usings + """
            class C
            {
                MulticastMSearch M() => new MulticastMSearch
                {
                    ST = new ST { StSearchType = STType.All },
                    CPFN = "CP",
                    TCPPORT = {|SSDP003:new DynamicPort(80)|}
                };
            }
            """);

    [Fact]
    public async Task DynamicPort_FromANonConstant_IsNotReported() =>
        await Verify.AnalyzerAsync(Usings + """
            class C
            {
                DynamicPort M(int port) => new DynamicPort(port);
            }
            """);

    // ---------------------------------------------------------------- SSDP005

    [Theory]
    [InlineData(16777216)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task ConfigId_OutsideTheRange_IsReported(int configId) =>
        await Verify.AnalyzerAsync(Usings + $$"""
            class C
            {
                RootDeviceConfiguration M() => new RootDeviceConfiguration
                {
                    {|SSDP005:CONFIGID = {{configId}}|}
                };
            }
            """);

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(16777215)]
    public async Task ConfigId_InsideTheRange_IsNotReported(int configId) =>
        await Verify.AnalyzerAsync(Usings + $$"""
            class C
            {
                RootDeviceConfiguration M() => new RootDeviceConfiguration
                {
                    CONFIGID = {{configId}}
                };
            }
            """);

    [Theory]
    [InlineData(8080)]
    [InlineData(80)]
    [InlineData(49151)]
    public async Task DeviceUnicastPort_OutsideTheAllowedPorts_IsReported(int port) =>
        await Verify.AnalyzerAsync(Usings + $$"""
            class C
            {
                RootDeviceConfiguration M() => new RootDeviceConfiguration
                {
                    IpEndPoint = {|SSDP005:new IPEndPoint(IPAddress.Loopback, {{port}})|}
                };
            }
            """);

    // 1900 is legal alongside the dynamic range, which is the branch a naive
    // range check would get wrong.
    [Theory]
    [InlineData(1900)]
    [InlineData(49152)]
    [InlineData(65535)]
    public async Task DeviceUnicastPort_OnAnAllowedPort_IsNotReported(int port) =>
        await Verify.AnalyzerAsync(Usings + $$"""
            class C
            {
                RootDeviceConfiguration M() => new RootDeviceConfiguration
                {
                    IpEndPoint = new IPEndPoint(IPAddress.Loopback, {{port}})
                };
            }
            """);

    [Fact]
    public async Task DeviceUnicastPort_FromANonConstant_IsNotReported() =>
        await Verify.AnalyzerAsync(Usings + """
            class C
            {
                RootDeviceConfiguration M(int port) => new RootDeviceConfiguration
                {
                    IpEndPoint = new IPEndPoint(IPAddress.Loopback, port)
                };
            }
            """);

    // ------------------------------------------------- false-positive budget

    [Fact]
    public async Task UnrelatedTypesWithTheSameShape_AreNotReported() =>
        await Verify.AnalyzerAsync(Usings + """
            namespace Other
            {
                public readonly struct MxSeconds { public MxSeconds(int s) { } }
                public readonly struct DynamicPort { public DynamicPort(int p) { } }
                public sealed record RootDeviceConfiguration { public int CONFIGID { get; init; } }
            }

            class C
            {
                Other.MxSeconds A() => new Other.MxSeconds(300);
                Other.DynamicPort B() => new Other.DynamicPort(80);
                Other.RootDeviceConfiguration D() => new Other.RootDeviceConfiguration { CONFIGID = 999999999 };
            }
            """);

    [Fact]
    public async Task CodeWithoutTheLibrary_IsNotReported() =>
        await Verify.AnalyzerAsync("""
            class C
            {
                int M() => 30;
            }
            """);

    [Fact]
    public async Task AConfigurationWithNoOffendingValues_IsNotReported() =>
        await Verify.AnalyzerAsync(Usings + """
            class C
            {
                RootDeviceConfiguration M() => new RootDeviceConfiguration
                {
                    DeviceUUID = "3fb1ba26-1f0c-4d4a-9a2d-1f6a3c9a0e11",
                    TypeName = "SampleRootDevice",
                    Version = 1,
                    CONFIGID = 100,
                    CacheControl = TimeSpan.FromSeconds(1800),
                    IpEndPoint = new IPEndPoint(IPAddress.Loopback, 1900)
                };
            }
            """);
}
