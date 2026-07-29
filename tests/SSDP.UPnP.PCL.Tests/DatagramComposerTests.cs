using System.Net;
using System.Text;
using SSDP.UPnP.PCL.Model;
using SSDP.UPnP.PCL.Parsing;
using Xunit;

namespace SSDP.UPnP.PCL.Tests;

public class DatagramComposerTests
{
    private static string[] HeaderLines(byte[] datagram)
    {
        var text = Encoding.UTF8.GetString(datagram);

        Assert.EndsWith("\r\n\r\n", text);

        return text[..^4].Split("\r\n");
    }

    [Fact]
    public void ComposeMSearchRequest_Multicast()
    {
        var request = new MulticastMSearch
        {
            MX = new MxSeconds(3),
            ST = new ST { StSearchType = STType.All },
            CPFN = "Test CP",
            UserAgent = new UserAgent
            {
                OperatingSystem = "Linux",
                OperatingSystemVersion = "6.1",
                UpnpMajorVersion = 2,
                UpnpMinorVersion = 0,
                ProductName = "Test",
                ProductVersion = "1.0"
            }
        };

        var lines = HeaderLines(DatagramComposer.ComposeMSearchRequest(request));

        Assert.Equal("M-SEARCH * HTTP/1.1", lines[0]);
        Assert.Contains("HOST: 239.255.255.250:1900", lines);
        Assert.Contains("MAN: \"ssdp:discover\"", lines);
        Assert.Contains("MX: 3", lines);
        Assert.Contains("ST: ssdp:all", lines);
        Assert.Contains("USER-AGENT: Linux/6.1 UPnP/2.0 Test/1.0", lines);
        Assert.Contains("CPFN.UPNP.ORG: Test CP", lines);
    }

    // UDA 2.0 section 1.3.2 makes MX >= 1 a "shall", and MxSeconds enforces it at
    // the point of assignment rather than leaving it for the composer to discover.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MxSeconds_BelowOne_Throws(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MxSeconds(seconds));
    }

    [Fact]
    public void MxSeconds_Default_IsTheLegalMinimum()
    {
        Assert.Equal(1, default(MxSeconds).Seconds);
    }

    [Fact]
    public void ComposeMSearchRequest_MulticastWithEmptyCpfn_Throws()
    {
        // required obliges the caller to set CPFN; this is the blank it cannot catch.
        var request = new MulticastMSearch
        {
            ST = new ST { StSearchType = STType.All },
            CPFN = "  "
        };

        Assert.Throws<SSDPException>(() => DatagramComposer.ComposeMSearchRequest(request));
    }

    [Fact]
    public void ComposeMSearchRequest_Unicast_OmitsMulticastHeaders()
    {
        var request = new UnicastMSearch
        {
            Target = new IPEndPoint(IPAddress.Parse("192.168.0.20"), 1900),
            ST = new ST { StSearchType = STType.RootDeviceSearch },
        };

        var lines = HeaderLines(DatagramComposer.ComposeMSearchRequest(request));

        // HOST is derived from Target, so the two can no longer disagree.
        Assert.Contains("HOST: 192.168.0.20:1900", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("MX:"));
        Assert.DoesNotContain(lines, line => line.StartsWith("CPFN.UPNP.ORG:"));
    }

    [Fact]
    public void ComposeMSearchResponse_AllHeaders()
    {
        var response = new MSearchResponse
        {
            StatusCode = 200,
            ResponseReason = "OK",
            MaxAge = TimeSpan.FromSeconds(1800),
            Date = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero),
            Location = new Uri("http://192.168.0.10/description.xml"),
            Server = new Server
            {
                OperatingSystem = "Linux",
                OperatingSystemVersion = "6.1",
                UpnpMajorVersion = 2,
                UpnpMinorVersion = 0,
                ProductName = "Test",
                ProductVersion = "1.0"
            },
            ST = new ST { StSearchType = STType.RootDeviceSearch, EntityType = EntityType.RootDevice },
            USN = new USN { EntityType = EntityType.RootDevice, DeviceUUID = "device-1" },
            BOOTID = 1721600000,
            CONFIGID = 77,
            SEARCHPORT = new DynamicPort(49152),
            SECURELOCATION = "https://192.168.0.10/description.xml"
        };

        var lines = HeaderLines(DatagramComposer.ComposeMSearchResponse(response));

        Assert.Equal("HTTP/1.1 200 OK", lines[0]);
        Assert.Contains("CACHE-CONTROL: max-age=1800", lines);
        Assert.Contains("DATE: Wed, 22 Jul 2026 10:00:00 GMT", lines);
        Assert.Contains("EXT:", lines);
        Assert.Contains("LOCATION: http://192.168.0.10/description.xml", lines);
        Assert.Contains("ST: upnp:rootdevice", lines);
        Assert.Contains("USN: uuid:device-1::upnp:rootdevice", lines);
        Assert.Contains("BOOTID.UPNP.ORG: 1721600000", lines);
        Assert.Contains("CONFIGID.UPNP.ORG: 77", lines);
        Assert.Contains("SEARCHPORT.UPNP.ORG: 49152", lines);
        Assert.Contains("SECURELOCATION.UPNP.ORG: https://192.168.0.10/description.xml", lines);
    }

    [Fact]
    public void ComposeNotify_Alive_IncludesCacheControlLocationAndServer()
    {
        var notify = new Notify
        {
            NTS = NTS.Alive,
            MaxAge = TimeSpan.FromSeconds(1800),
            Location = new Uri("http://192.168.0.10/description.xml"),
            NT = "upnp:rootdevice",
            Server = new Server { OperatingSystem = "Linux", OperatingSystemVersion = "6.1", ProductName = "Test", ProductVersion = "1.0" },
            USN = new USN { EntityType = EntityType.RootDevice, DeviceUUID = "device-1" },
            BOOTID = 7,
            CONFIGID = 1,
            SECURELOCATION = "https://192.168.0.10/description.xml"
        };

        var lines = HeaderLines(DatagramComposer.ComposeNotify(notify));

        Assert.Equal("NOTIFY * HTTP/1.1", lines[0]);
        Assert.Contains("HOST: 239.255.255.250:1900", lines);
        Assert.Contains("CACHE-CONTROL: max-age=1800", lines);
        Assert.Contains("LOCATION: http://192.168.0.10/description.xml", lines);
        Assert.Contains("NT: upnp:rootdevice", lines);
        Assert.Contains("NTS: ssdp:alive", lines);
        Assert.Contains("USN: uuid:device-1::upnp:rootdevice", lines);
        Assert.Contains("BOOTID.UPNP.ORG: 7", lines);
        Assert.Contains("SECURELOCATION.UPNP.ORG: https://192.168.0.10/description.xml", lines);
        Assert.Contains(lines, line => line.StartsWith("SERVER: "));
    }

    [Fact]
    public void ComposeNotify_ByeBye_OmitsAliveOnlyHeaders()
    {
        var notify = new Notify
        {
            NTS = NTS.ByeBye,
            MaxAge = TimeSpan.FromSeconds(1800),
            Location = new Uri("http://192.168.0.10/description.xml"),
            NT = "uuid:device-1",
            Server = new Server(),
            USN = new USN { EntityType = EntityType.Device, DeviceUUID = "device-1" },
            SECURELOCATION = "https://192.168.0.10/x"
        };

        var lines = HeaderLines(DatagramComposer.ComposeNotify(notify));

        Assert.Contains("NTS: ssdp:byebye", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("CACHE-CONTROL:"));
        Assert.DoesNotContain(lines, line => line.StartsWith("LOCATION:"));
        Assert.DoesNotContain(lines, line => line.StartsWith("SERVER:"));
        Assert.DoesNotContain(lines, line => line.StartsWith("SECURELOCATION.UPNP.ORG:"));
    }

    [Fact]
    public void ComposeNotify_Update_IncludesNextBootId()
    {
        var notify = new Notify
        {
            NTS = NTS.Update,
            Location = new Uri("http://192.168.0.10/description.xml"),
            NT = "upnp:rootdevice",
            USN = new USN { EntityType = EntityType.RootDevice, DeviceUUID = "device-1" },
            BOOTID = 7,
            NEXTBOOTID = 8
        };

        var lines = HeaderLines(DatagramComposer.ComposeNotify(notify));

        Assert.Contains("NTS: ssdp:update", lines);
        Assert.Contains("NEXTBOOTID.UPNP.ORG: 8", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("CACHE-CONTROL:"));
    }

    [Fact]
    public void ComposeNotify_TakesTheAdvertisedLifetimeFromMaxAge()
    {
        var notify = AliveNotifyWith(TimeSpan.FromSeconds(1800));

        Assert.Contains("CACHE-CONTROL: max-age=1800", HeaderLines(DatagramComposer.ComposeNotify(notify)));
    }

    [Fact]
    public void ComposeNotify_WithoutMaxAge_StillCarriesTheRequiredHeader()
    {
        // CACHE-CONTROL is Required on ssdp:alive (UDA 2.0 section 1.2.2), so a
        // notification that announces no lifetime emits zero rather than omitting
        // the header and composing a non-conforming message.
        var notify = AliveNotifyWith(maxAge: null);

        Assert.Contains("CACHE-CONTROL: max-age=0", HeaderLines(DatagramComposer.ComposeNotify(notify)));
    }

    private static Notify AliveNotifyWith(TimeSpan? maxAge) => new()
    {
        NTS = NTS.Alive,
        MaxAge = maxAge,
        Location = new Uri("http://192.168.0.10/description.xml"),
        NT = "upnp:rootdevice",
        Server = new Server(),
        USN = new USN { EntityType = EntityType.RootDevice, DeviceUUID = "device-1" }
    };

    // Listening on 1900 is said with a null SEARCHPORT rather than by writing 1900
    // into a field that no longer accepts it - DynamicPort's range starts at 49152,
    // so the composer no longer needs a "not 1900" special case at all.
    [Fact]
    public void ComposeNotify_WithoutSearchPort_OmitsTheHeader()
    {
        var notify = AliveNotifyWith(TimeSpan.FromSeconds(1800)) with { SEARCHPORT = null };

        var lines = HeaderLines(DatagramComposer.ComposeNotify(notify));

        Assert.DoesNotContain(lines, line => line.StartsWith("SEARCHPORT.UPNP.ORG:"));
    }

    [Fact]
    public void ComposeNotify_WithSearchPort_IncludesIt()
    {
        var notify = AliveNotifyWith(TimeSpan.FromSeconds(1800)) with { SEARCHPORT = new DynamicPort(49152) };

        Assert.Contains("SEARCHPORT.UPNP.ORG: 49152", HeaderLines(DatagramComposer.ComposeNotify(notify)));
    }

    [Fact]
    public void DeviceInfo_ToHeaderString_WithUnsetFields_PinsCurrentShape()
    {
        Assert.Equal("/ UPnP/2.0 /", new Server().ToHeaderString());
    }

    [Fact]
    public void ComposedRequest_ParsesBackWithSameValues()
    {
        var request = new MulticastMSearch
        {
            MX = new MxSeconds(2),
            ST = new ST { StSearchType = STType.DeviceTypeSearch, TypeName = "MediaServer", Version = 3, EntityType = EntityType.DeviceType },
            CPFN = "Test CP"
        };

        var text = Encoding.UTF8.GetString(DatagramComposer.ComposeMSearchRequest(request));
        var headers = text.Split("\r\n")
            .Skip(1)
            .Where(line => line.Contains(": "))
            .Select(line => line.Split(": ", 2))
            .ToDictionary(pair => pair[0], pair => pair[1], StringComparer.OrdinalIgnoreCase);

        Assert.Equal("urn:schemas-upnp-org:device:MediaServer:3", headers["ST"]);
        Assert.Equal("2", headers["MX"]);

        var roundTripped = ST.Parse(headers["ST"]);
        Assert.True(roundTripped.IsSuccess);
        Assert.Equal(request.ST.TypeName, roundTripped.Value.TypeName);
        Assert.Equal(request.ST.Version, roundTripped.Value.Version);
    }

    // A required header with an empty value is worse than one that is missing: it
    // is syntactically present, so a strict device reads it as "the sender declares
    // it has no host". These are the two places the composer used to do that.
    [Fact]
    public void ComposeNotify_UnicastWithoutHost_Throws()
    {
        var notify = AliveNotifyWith(TimeSpan.FromSeconds(1800)) with
        {
            NotifyTransportType = TransportType.Unicast,
            HOST = null
        };

        Assert.Throws<SSDPException>(() => DatagramComposer.ComposeNotify(notify));
    }

    [Fact]
    public void ComposeNotify_UnicastWithHost_UsesIt()
    {
        var notify = AliveNotifyWith(TimeSpan.FromSeconds(1800)) with
        {
            NotifyTransportType = TransportType.Unicast,
            HOST = "192.168.0.20:1900"
        };

        Assert.Contains("HOST: 192.168.0.20:1900", HeaderLines(DatagramComposer.ComposeNotify(notify)));
    }

    // LOCATION is Required on a search response (UDA 2.0 section 1.3.3), and is now
    // required by the type - so the empty-value case is a compile error rather than
    // a test. What is left to pin is that a real one still reaches the wire.
    [Fact]
    public void ComposeMSearchResponse_CarriesTheLocation()
    {
        var response = new MSearchResponse
        {
            Location = new Uri("http://192.168.0.10/description.xml"),
            ST = new ST { StSearchType = STType.RootDeviceSearch, EntityType = EntityType.RootDevice },
            USN = new USN { EntityType = EntityType.RootDevice, DeviceUUID = "device-1" }
        };

        Assert.Contains(
            "LOCATION: http://192.168.0.10/description.xml",
            HeaderLines(DatagramComposer.ComposeMSearchResponse(response)));
    }
}
