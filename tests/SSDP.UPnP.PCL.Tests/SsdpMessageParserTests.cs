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
        Assert.True(request.UserAgent.SupportsAtLeast(2));
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
            Assert.Equal(int.Parse(tcpPort), result.Value.TCPPORT?.Port);
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
        Assert.Equal(TimeSpan.FromSeconds(1800), response.MaxAge);
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
    // The five situations ParseMaxAge collapses into 0, kept apart here.
    [InlineData("max-age=0", 0)]          // the device genuinely said zero
    [InlineData("max-age=-1", null)]      // invalid value
    [InlineData("max-age=abc", null)]     // unparsable value
    [InlineData("no-cache", null)]        // header present, no max-age directive
    [InlineData(null, null)]              // no CACHE-CONTROL at all
    // And the ordinary cases, which must agree with ParseMaxAge.
    [InlineData("max-age=1800", 1800)]
    [InlineData("max-age=1800, must-revalidate", 1800)]
    [InlineData("public, max-age=1800, s-maxage=600", 1800)]
    [InlineData("s-maxage=600", null)]
    [InlineData("", null)]
    public void TryParseMaxAge_SeparatesAbsentFromZero(string? cacheControl, int? expected)
    {
        Assert.Equal(expected, SsdpMessageParser.TryParseMaxAge(cacheControl));
    }

    [Theory]
    [InlineData("max-age=0")]
    [InlineData("max-age=-1")]
    [InlineData("max-age=abc")]
    [InlineData("no-cache")]
    [InlineData(null)]
    [InlineData("max-age=1800")]
    [InlineData("public, max-age=1800, s-maxage=600")]
    public void ParseMaxAge_StillAgreesWithTryParse_TreatingAbsentAsZero(string? cacheControl)
    {
        // ParseMaxAge is public API in use: its behaviour must not drift, so it is
        // exactly TryParseMaxAge with absence flattened to 0.
        Assert.Equal(
            SsdpMessageParser.TryParseMaxAge(cacheControl) ?? 0,
            SsdpMessageParser.ParseMaxAge(cacheControl));
    }

    [Fact]
    public void MaxAge_IsZeroWhenTheDeviceSaidZero_ButNullWhenItSaidNothing()
    {
        // The pair that is the entire point of the change: expiring immediately and
        // announcing no lifetime call for opposite handling downstream.
        var saidZero = SsdpMessageParser.ParseMSearchResponse(Message(MessageType.Response,
            new Dictionary<string, string>
            {
                ["CACHE-CONTROL"] = "max-age=0",
                ["ST"] = "upnp:rootdevice",
                ["USN"] = "uuid:device-1::upnp:rootdevice"
            }));

        var saidNothing = SsdpMessageParser.ParseMSearchResponse(Message(MessageType.Response,
            new Dictionary<string, string>
            {
                ["ST"] = "upnp:rootdevice",
                ["USN"] = "uuid:device-1::upnp:rootdevice"
            }));

        Assert.True(saidZero.IsSuccess);
        Assert.True(saidNothing.IsSuccess);

        Assert.Equal(TimeSpan.Zero, saidZero.Value.MaxAge);
        Assert.Null(saidNothing.Value.MaxAge);
    }

    [Fact]
    public void Notify_ByeBye_HasNoMaxAge()
    {
        // A byebye carries no CACHE-CONTROL, so null is normal rather than a gap.
        var result = SsdpMessageParser.ParseNotify(Message(MessageType.Request,
            new Dictionary<string, string>
            {
                ["NT"] = "upnp:rootdevice",
                ["NTS"] = "ssdp:byebye",
                ["USN"] = "uuid:device-1::upnp:rootdevice"
            }, method: "NOTIFY"));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.MaxAge);
    }

    [Fact]
    public void Notify_Alive_CarriesTheAnnouncedMaxAge()
    {
        var result = SsdpMessageParser.ParseNotify(Message(MessageType.Request,
            new Dictionary<string, string>
            {
                ["CACHE-CONTROL"] = "max-age=1800, must-revalidate",
                ["NT"] = "upnp:rootdevice",
                ["NTS"] = "ssdp:alive",
                ["USN"] = "uuid:device-1::upnp:rootdevice"
            }, method: "NOTIFY"));

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromSeconds(1800), result.Value.MaxAge);
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
        Assert.Equal(TimeSpan.FromSeconds(1800), result.Value.MaxAge);
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
    // The spec's own example (UDA 2.0 section 1.1.2) and the ordinary forms.
    [InlineData("unix/5.1 UPnP/2.0 MyProduct/1.0", 2, 0)]
    [InlineData("Windows/10.0 UPnP/2.0 Product/1.5", 2, 0)]
    [InlineData("Linux/4.4 UPnP/1.0 Sonos/81.1-56180", 1, 0)]
    [InlineData("Linux/6.1 UPnP/1.1 Thing/2", 1, 1)]
    // Reordered or short forms real devices send: the UPnP token is found by
    // name, and a product token in second position is no longer mistaken for it.
    [InlineData("UPnP/1.0, DLNADOC/1.50 Platinum/1.0.5.13", 1, 0)]
    [InlineData("Windows NT/5.1, UPnP/1.0", 1, 0)]
    [InlineData("UPnP/1.1", 1, 1)]
    [InlineData("upnp/1.0 Something/2", 1, 0)]
    // No UPnP token, or an unusable one: absence is reported, never assumed.
    [InlineData("Windows/10.0 Product/1.5", null, null)]
    [InlineData("BareOs", null, null)]
    [InlineData("Linux/6.1 UPnP/notaversion Thing/2", null, null)]
    [InlineData("Linux/6.1 UPnP/2 Thing/2", null, null)]
    public void ParseDeviceInfo_FindsTheUpnpVersionByName(string value, int? major, int? minor)
    {
        var info = SsdpMessageParser.ParseDeviceInfo<Server>(value);

        Assert.Equal(major, info.UpnpMajorVersion);
        Assert.Equal(minor, info.UpnpMinorVersion);
        Assert.Equal(value, info.FullString);
    }

    [Theory]
    [InlineData("Windows/10.0 UPnP/2.0 Product/1.5", "Windows", "10.0", "Product", "1.5")]
    [InlineData("Linux/6.1 UPnP/1.1 Thing/2", "Linux", "6.1", "Thing", "2")]
    [InlineData("BareOs", "BareOs", null, null, null)]
    public void ParseDeviceInfo_KeepsPositionalOsAndProduct(
        string value, string os, string? osVersion, string? product, string? productVersion)
    {
        var info = SsdpMessageParser.ParseDeviceInfo<Server>(value);

        Assert.Equal(os, info.OperatingSystem);
        Assert.Equal(osVersion, info.OperatingSystemVersion);
        Assert.Equal(product, info.ProductName);
        Assert.Equal(productVersion, info.ProductVersion);
    }

    [Fact]
    public void ParseDeviceInfo_AbsentHeader_ClaimsNoVersion()
    {
        // Previously an empty Server reported UPnP 2.0, turning "said nothing"
        // into the strongest possible claim.
        var info = SsdpMessageParser.ParseDeviceInfo<Server>(null);

        Assert.Null(info.UpnpMajorVersion);
        Assert.Null(info.UpnpMinorVersion);
        Assert.Null(info.FullString);
        Assert.False(info.SupportsAtLeast(1));
    }

    [Theory]
    [InlineData("Linux/4.4 UPnP/1.0 X/1", 1, 1, false)]
    [InlineData("Linux/4.4 UPnP/1.1 X/1", 1, 1, true)]
    [InlineData("Linux/4.4 UPnP/2.0 X/1", 1, 1, true)]
    [InlineData("Linux/4.4 UPnP/2.0 X/1", 2, 0, true)]
    [InlineData("Linux/4.4 UPnP/1.1 X/1", 2, 0, false)]
    public void SupportsAtLeast_ComparesVersions(string value, int major, int minor, bool expected)
    {
        var info = SsdpMessageParser.ParseDeviceInfo<Server>(value);

        Assert.Equal(expected, info.SupportsAtLeast(major, minor));
    }

    [Fact]
    public void ParseRfc1123Date_Invalid_ReturnsNull()
    {
        Assert.Null(SsdpMessageParser.ParseRfc1123Date("not a date"));
        Assert.Null(SsdpMessageParser.ParseRfc1123Date(null));
    }

    // A real M-SEARCH response captured from a Platinum/1.0.5.13 renderer: a
    // UPnP 1.0 device, so no BOOTID, and the boot signature arrives as an
    // RFC 2774 namespaced NLS header. Mixed-case header names are as captured.
    private static HttpRequestResponse PlatinumResponse() => new()
    {
        MessageType = MessageType.Response,
        StatusCode = 200,
        ReasonPhrase = "OK",
        Transport = HttpTransport.Udp,
        Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Location"] = "http://192.168.0.217:16422",
            ["Cache-Control"] = "max-age=66",
            ["Server"] = "UPnP/1.0, DLNADOC/1.50 Platinum/1.0.5.13",
            ["EXT"] = "",
            ["OPT"] = "\"http://schemas.upnp.org/upnp/1/0/\"; ns=01",
            ["01-NLS"] = "1785066224",
            ["USN"] = "uuid:bf3f7ffd-777e-4f76-bfb8-b7ff6be2befe::upnp:rootdevice",
            ["ST"] = "upnp:rootdevice",
            ["Date"] = "Sun, 26 Jul 2026 14:40:08 GMT"
        }
    };

    [Fact]
    public void ParseMSearchResponse_Upnp10Device_ReportsNoBootIdAndAnNls()
    {
        var result = SsdpMessageParser.ParseMSearchResponse(PlatinumResponse());

        Assert.True(result.IsSuccess);

        var response = result.Value;
        Assert.Null(response.BOOTID);
        Assert.Equal("1785066224", response.NLS);
        Assert.Equal(TimeSpan.FromSeconds(66), response.MaxAge);
        Assert.Equal(new Uri("http://192.168.0.217:16422"), response.Location);
        Assert.Equal("bf3f7ffd-777e-4f76-bfb8-b7ff6be2befe", response.USN?.DeviceUUID);
        Assert.False(response.HasParsingError);
    }

    [Fact]
    public void ParseMSearchResponse_Uda20DeviceWithBootIdZero_IsDistinctFromAbsent()
    {
        // The whole point of the nullable change: "sent 0" and "sent nothing" are
        // different states, and only the former carries a BOOTID.
        var message = Message(MessageType.Response, new Dictionary<string, string>
        {
            ["ST"] = "upnp:rootdevice",
            ["USN"] = "uuid:device-1::upnp:rootdevice",
            ["BOOTID.UPNP.ORG"] = "0"
        });

        var result = SsdpMessageParser.ParseMSearchResponse(message);

        Assert.True(result.IsSuccess);
        Assert.Equal(0u, result.Value.BOOTID);
        Assert.Null(result.Value.NLS);
    }

    [Fact]
    public void ParseNotify_CarriesNlsAndAbsentBootId()
    {
        var message = Message(MessageType.Request, new Dictionary<string, string>
        {
            ["NT"] = "upnp:rootdevice",
            ["NTS"] = "ssdp:alive",
            ["USN"] = "uuid:device-1::upnp:rootdevice",
            ["OPT"] = "\"http://schemas.upnp.org/upnp/1/0/\"; ns=01",
            ["01-NLS"] = "d1b6a3f0-0c5e-4d0a-9a3e-4b5f6c7d8e9f"
        }, method: "NOTIFY");

        var result = SsdpMessageParser.ParseNotify(message);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.BOOTID);
        // Opaque: GUID-shaped values must survive intact, not be coerced.
        Assert.Equal("d1b6a3f0-0c5e-4d0a-9a3e-4b5f6c7d8e9f", result.Value.NLS);
    }

    [Fact]
    public void ParseNls_HonoursTheDeclaredNamespacePrefix()
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OPT"] = "\"http://schemas.upnp.org/upnp/1/0/\"; ns=02",
            ["01-NLS"] = "wrong",
            ["02-NLS"] = "right"
        };

        Assert.Equal("right", SsdpMessageParser.ParseNls(headers));
    }

    [Fact]
    public void ParseNls_WithoutOpt_FallsBackToAnyNamespacedHeader()
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["01-NLS"] = "1785066224"
        };

        Assert.Equal("1785066224", SsdpMessageParser.ParseNls(headers));
    }

    [Fact]
    public void ParseNls_ForeignNamespaceOpt_IsNotTrustedForTheMapping()
    {
        // The OPT belongs to some other extension, so its ns= must not be used to
        // resolve NLS. The header name itself is still a valid signal, so the
        // lenient fallback applies.
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OPT"] = "\"http://example.com/other-extension/\"; ns=02",
            ["02-NLS"] = "from-fallback"
        };

        Assert.Equal("from-fallback", SsdpMessageParser.ParseNls(headers));
    }

    [Fact]
    public void ParseNls_MultipleNamespacedHeaders_ResolveDeterministically()
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["03-NLS"] = "third",
            ["01-NLS"] = "first",
            ["02-NLS"] = "second"
        };

        Assert.Equal("first", SsdpMessageParser.ParseNls(headers));
    }

    [Fact]
    public void ParseNls_AbsentIsNull()
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ST"] = "upnp:rootdevice"
        };

        Assert.Null(SsdpMessageParser.ParseNls(headers));
    }

    [Fact]
    public void ParseMSearchResponse_LeavesOptAndNlsInTheExtraHeaders()
    {
        // Dynamically named headers cannot live in the standard-header filter set,
        // so consumers keep reaching them through Headers.
        var result = SsdpMessageParser.ParseMSearchResponse(PlatinumResponse());

        Assert.True(result.IsSuccess);
        Assert.Contains("OPT", result.Value.Headers.Keys);
        Assert.Contains("01-NLS", result.Value.Headers.Keys);
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
