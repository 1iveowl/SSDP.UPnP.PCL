using System.Net;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Model;
using SSDP.UPnP.PCL.Parsing;
using Xunit;

namespace SSDP.UPnP.PCL.Tests;

public class SsdpMessageParserTests
{
    private static HttpRequestResponse Message(
        MessageType messageType,
        Dictionary<string, string> headers,
        string? method = null,
        HttpTransport transport = HttpTransport.Udp) => new()
    {
        MessageType = messageType,
        Method = method,
        Transport = transport,
        Headers = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase),
        LocalEndPoint = new IPEndPoint(IPAddress.Parse("192.168.0.10"), 1900),
        RemoteEndPoint = new IPEndPoint(IPAddress.Parse("192.168.0.20"), 40000)
    };

    [Fact]
    public void ParseMSearchRequest_ReadsAllStandardHeaders()
    {
        var message = Message(MessageType.Request, new Dictionary<string, string>
        {
            ["HOST"] = "239.255.255.250:1900",
            ["MAN"] = "\"ssdp:discover\"",
            ["MX"] = "3",
            ["ST"] = "ssdp:all",
            ["USER-AGENT"] = "Linux/6.1 UPnP/2.0 TestProduct/1.0",
            ["CPFN.UPNP.ORG"] = "Test Control Point",
            ["CPUUID.UPNP.ORG"] = "cp-uuid-1",
            ["X-CUSTOM"] = "custom-value"
        }, method: "M-SEARCH");

        var result = SsdpMessageParser.ParseMSearchRequest(message);

        Assert.True(result.IsSuccess);

        var request = result.Value;
        Assert.Equal(TransportType.Multicast, request.TransportType);
        Assert.Equal(TimeSpan.FromSeconds(3), request.MX);
        Assert.Equal(STType.All, request.ST.StSearchType);
        Assert.Equal("Test Control Point", request.CPFN);
        Assert.Equal("cp-uuid-1", request.CPUUID);
        Assert.Equal("Linux", request.UserAgent.OperatingSystem);
        Assert.Equal("TestProduct", request.UserAgent.ProductName);
        Assert.True(request.UserAgent.IsUpnp2);
        Assert.Equal(new IPEndPoint(IPAddress.Parse("192.168.0.20"), 40000), request.RemoteIpEndPoint);

        var (key, value) = Assert.Single(request.Headers);
        Assert.Equal("X-CUSTOM", key);
        Assert.Equal("custom-value", value);
    }

    [Fact]
    public void ParseMSearchRequest_InvalidST_Fails()
    {
        var message = Message(MessageType.Request, new Dictionary<string, string>
        {
            ["ST"] = "nonsense"
        }, method: "M-SEARCH");

        var result = SsdpMessageParser.ParseMSearchRequest(message);

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData("\"ssdp:discover\"", true)]
    [InlineData("ssdp:discover", true)] // tolerated unquoted form
    [InlineData("\"ssdp:wrong\"", false)]
    [InlineData("", false)]
    public void ParseMSearchRequest_ValidatesMan(string man, bool expectSuccess)
    {
        var headers = new Dictionary<string, string>
        {
            ["HOST"] = "239.255.255.250:1900",
            ["MX"] = "2",
            ["ST"] = "ssdp:all"
        };

        if (man.Length > 0)
        {
            headers["MAN"] = man;
        }

        var result = SsdpMessageParser.ParseMSearchRequest(Message(MessageType.Request, headers, method: "M-SEARCH"));

        Assert.Equal(expectSuccess, result.IsSuccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void ParseMSearchRequest_MulticastWithoutValidMx_Fails(string? mx)
    {
        var headers = new Dictionary<string, string>
        {
            ["HOST"] = "239.255.255.250:1900",
            ["MAN"] = "\"ssdp:discover\"",
            ["ST"] = "ssdp:all"
        };

        if (mx is not null)
        {
            headers["MX"] = mx;
        }

        var result = SsdpMessageParser.ParseMSearchRequest(Message(MessageType.Request, headers, method: "M-SEARCH"));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ParseMSearchRequest_UnicastHost_NeedsNoMx()
    {
        var message = Message(MessageType.Request, new Dictionary<string, string>
        {
            ["HOST"] = "192.168.0.10:1900",
            ["MAN"] = "\"ssdp:discover\"",
            ["ST"] = "upnp:rootdevice"
        }, method: "M-SEARCH");

        var result = SsdpMessageParser.ParseMSearchRequest(message);

        Assert.True(result.IsSuccess);
        Assert.Equal(TransportType.Unicast, result.Value.TransportType);
        Assert.Equal(TimeSpan.Zero, result.Value.MX);
    }

    [Theory]
    [InlineData("51000", true)]
    [InlineData("49152", true)]
    [InlineData("65535", true)]
    [InlineData("1234", false)]  // below the RFC 4340 dynamic range
    [InlineData("65536", false)]
    [InlineData("abc", false)]
    public void ParseMSearchRequest_ValidatesTcpPort(string tcpPort, bool expectSuccess)
    {
        var message = Message(MessageType.Request, new Dictionary<string, string>
        {
            ["HOST"] = "239.255.255.250:1900",
            ["MAN"] = "\"ssdp:discover\"",
            ["MX"] = "2",
            ["ST"] = "ssdp:all",
            ["TCPPORT.UPNP.ORG"] = tcpPort
        }, method: "M-SEARCH");

        var result = SsdpMessageParser.ParseMSearchRequest(message);

        if (expectSuccess)
        {
            Assert.True(result.IsSuccess);
            Assert.Equal(int.Parse(tcpPort), result.Value.TCPPORT);
        }
        else
        {
            Assert.False(result.IsSuccess);
        }
    }

    [Fact]
    public void ParseMSearchResponse_ReadsAllStandardHeaders()
    {
        var message = Message(MessageType.Response, new Dictionary<string, string>
        {
            ["CACHE-CONTROL"] = "max-age=1800",
            ["DATE"] = "Wed, 22 Jul 2026 10:00:00 GMT",
            ["EXT"] = "",
            ["LOCATION"] = "http://192.168.0.20/description.xml",
            ["SERVER"] = "Linux/6.1 UPnP/2.0 TestDevice/1.0",
            ["ST"] = "upnp:rootdevice",
            ["USN"] = "uuid:device-1::upnp:rootdevice",
            ["BOOTID.UPNP.ORG"] = "1721600000",
            ["CONFIGID.UPNP.ORG"] = "77",
            ["SEARCHPORT.UPNP.ORG"] = "1901"
        });

        var result = SsdpMessageParser.ParseMSearchResponse(message);

        Assert.True(result.IsSuccess);

        var response = result.Value;
        Assert.Equal(TimeSpan.FromSeconds(1800), response.CacheControl);
        Assert.Equal(new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero), response.Date);
        Assert.True(response.Ext);
        Assert.Equal(new Uri("http://192.168.0.20/description.xml"), response.Location);
        Assert.Equal(STType.RootDeviceSearch, response.ST?.StSearchType);
        Assert.Equal("device-1", response.USN?.DeviceUUID);
        Assert.Equal(1721600000u, response.BOOTID);
        Assert.Equal(77, response.CONFIGID);
        Assert.Equal(1901, response.SEARCHPORT);
        Assert.Equal("TestDevice", response.Server.ProductName);
    }

    [Theory]
    // Values that parsed before the multi-directive fix must keep their value.
    [InlineData("max-age=30", 30)]
    [InlineData("max-age = 30", 30)]
    [InlineData("max-age=1800", 1800)]
    [InlineData("MAX-AGE=1800", 1800)]
    [InlineData("no-cache, max-age=1800", 1800)]
    // These returned 0 before: any second directive broke the parse.
    [InlineData("max-age=1800, must-revalidate", 1800)]
    [InlineData("public, max-age=1800, s-maxage=600", 1800)]
    [InlineData("max-age=1800,must-revalidate", 1800)]
    [InlineData("must-revalidate, MAX-AGE = 1800 , public", 1800)]
    // s-maxage must not be mistaken for max-age.
    [InlineData("s-maxage=600", 0)]
    // Malformed but observed in the wild; lenient on receive.
    [InlineData("max-age=\"1800\"", 1800)]
    // Absent or unparsable.
    [InlineData("no-cache", 0)]
    [InlineData("max-age=", 0)]
    [InlineData("max-age=abc", 0)]
    [InlineData("max-age=-1", 0)]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData(null, 0)]
    public void ParseMaxAge_Variants(string? cacheControl, int expectedSeconds)
    {
        Assert.Equal(expectedSeconds, SsdpMessageParser.ParseMaxAge(cacheControl));
    }

    [Theory]
    [InlineData("max-age=99999999999")]
    [InlineData("max-age=2147483648")]
    [InlineData("max-age=999999999999999999999999999999")]
    public void ParseMaxAge_ClampsOversizedDeltaSeconds(string cacheControl)
    {
        Assert.Equal(int.MaxValue, SsdpMessageParser.ParseMaxAge(cacheControl));
    }

    [Fact]
    public void ParseMSearchResponse_MultiDirectiveCacheControl_KeepsTheDeviceLifetime()
    {
        var message = Message(MessageType.Response, new Dictionary<string, string>
        {
            ["CACHE-CONTROL"] = "max-age=1800, must-revalidate",
            ["ST"] = "upnp:rootdevice",
            ["USN"] = "uuid:device-1::upnp:rootdevice"
        });

        var result = SsdpMessageParser.ParseMSearchResponse(message);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromSeconds(1800), result.Value.CacheControl);
    }

    [Theory]
    [InlineData("ssdp:alive", NTS.Alive)]
    [InlineData("ssdp:byebye", NTS.ByeBye)]
    [InlineData("ssdp:update", NTS.Update)]
    [InlineData("upnp:propchange", NTS.Propchange)]
    [InlineData("something-else", NTS.Unknown)]
    public void ParseNotify_MapsNts(string nts, NTS expected)
    {
        var message = Message(MessageType.Request, new Dictionary<string, string>
        {
            ["NT"] = "upnp:rootdevice",
            ["NTS"] = nts,
            ["USN"] = "uuid:device-1::upnp:rootdevice"
        }, method: "NOTIFY");

        var result = SsdpMessageParser.ParseNotify(message);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.NTS);
    }

    [Fact]
    public void ParseNotify_GuidUuid_IsUpnp2Compliant()
    {
        var uuid = Guid.NewGuid().ToString();

        var message = Message(MessageType.Request, new Dictionary<string, string>
        {
            ["NT"] = $"uuid:{uuid}",
            ["NTS"] = "ssdp:alive",
            ["USN"] = $"uuid:{uuid}",
            ["NEXTBOOTID.UPNP.ORG"] = "42"
        }, method: "NOTIFY");

        var result = SsdpMessageParser.ParseNotify(message);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsUuidUpnp2Compliant);
        Assert.Equal(42u, result.Value.NEXTBOOTID);
    }

    [Fact]
    public void ParseNotify_NonGuidUuid_IsNotUpnp2Compliant()
    {
        var message = Message(MessageType.Request, new Dictionary<string, string>
        {
            ["NT"] = "uuid:legacy-name",
            ["NTS"] = "ssdp:alive",
            ["USN"] = "uuid:legacy-name"
        }, method: "NOTIFY");

        var result = SsdpMessageParser.ParseNotify(message);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsUuidUpnp2Compliant);
    }

    [Theory]
    [InlineData("Windows/10.0 UPnP/2.0 Product/1.5", "Windows", "10.0", "2", "0", "Product", "1.5", true)]
    [InlineData("Linux/6.1 UPnP/1.1 Thing/2", "Linux", "6.1", "1", "1", "Thing", "2", false)]
    [InlineData("BareOs", "BareOs", null, "1", "0", null, null, false)]
    public void ParseDeviceInfo_Variants(
        string value,
        string os,
        string? osVersion,
        string upnpMajor,
        string upnpMinor,
        string? product,
        string? productVersion,
        bool isUpnp2)
    {
        var info = SsdpMessageParser.ParseDeviceInfo<Server>(value);

        Assert.Equal(os, info.OperatingSystem);
        Assert.Equal(osVersion, info.OperatingSystemVersion);
        Assert.Equal(upnpMajor, info.UpnpMajorVersion);
        Assert.Equal(upnpMinor, info.UpnpMinorVersion);
        Assert.Equal(product, info.ProductName);
        Assert.Equal(productVersion, info.ProductVersion);
        Assert.Equal(isUpnp2, info.IsUpnp2);
        Assert.Equal(value, info.FullString);
    }

    [Fact]
    public void ParseRfc1123Date_Invalid_ReturnsMinValue()
    {
        Assert.Equal(DateTimeOffset.MinValue, SsdpMessageParser.ParseRfc1123Date("not a date"));
        Assert.Equal(DateTimeOffset.MinValue, SsdpMessageParser.ParseRfc1123Date(null));
    }

    [Fact]
    public void ParseMSearchResponse_GarbageStButValidUsn_IsLenient()
    {
        var message = Message(MessageType.Response, new Dictionary<string, string>
        {
            ["ST"] = "garbage",
            ["USN"] = "uuid:device-1::upnp:rootdevice"
        });

        var result = SsdpMessageParser.ParseMSearchResponse(message);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ST);
        Assert.Equal("device-1", result.Value.USN?.DeviceUUID);
    }

    [Fact]
    public void ParseMSearchResponse_NeitherStNorUsnParsable_Fails()
    {
        var message = Message(MessageType.Response, new Dictionary<string, string>
        {
            ["ST"] = "garbage",
            ["USN"] = "also-garbage"
        });

        var result = SsdpMessageParser.ParseMSearchResponse(message);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ParseNotify_GarbageUsn_IsLenient()
    {
        var message = Message(MessageType.Request, new Dictionary<string, string>
        {
            ["NT"] = "upnp:rootdevice",
            ["NTS"] = "ssdp:alive",
            ["USN"] = "garbage"
        }, method: "NOTIFY");

        var result = SsdpMessageParser.ParseNotify(message);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.USN);
        Assert.False(result.Value.IsUuidUpnp2Compliant);
    }
}
