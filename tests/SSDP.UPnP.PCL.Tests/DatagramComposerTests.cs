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
        var request = new MSearchRequest
        {
            TransportType = TransportType.Multicast,
            MX = TimeSpan.FromSeconds(3),
            ST = new ST { StSearchType = STType.All },
            CPFN = "Test CP",
            UserAgent = new UserAgent
            {
                OperatingSystem = "Linux",
                OperatingSystemVersion = "6.1",
                UpnpMajorVersion = "2",
                UpnpMinorVersion = "0",
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

    [Fact]
    public void ComposeMSearchRequest_Unicast_OmitsMulticastHeaders()
    {
        var request = new MSearchRequest
        {
            TransportType = TransportType.Unicast,
            HOST = "192.168.0.20:1900",
            ST = new ST { StSearchType = STType.RootDeviceSearch },
        };

        var lines = HeaderLines(DatagramComposer.ComposeMSearchRequest(request));

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
            CacheControl = TimeSpan.FromSeconds(1800),
            Date = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero),
            Location = new Uri("http://192.168.0.10/description.xml"),
            Server = new Server
            {
                OperatingSystem = "Linux",
                OperatingSystemVersion = "6.1",
                UpnpMajorVersion = "2",
                UpnpMinorVersion = "0",
                ProductName = "Test",
                ProductVersion = "1.0"
            },
            ST = new ST { StSearchType = STType.RootDeviceSearch, EntityType = EntityType.RootDevice },
            USN = new USN { EntityType = EntityType.RootDevice, DeviceUUID = "device-1" },
            BOOTID = 1721600000,
            CONFIGID = 77,
            SEARCHPORT = 1901,
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
        Assert.Contains("SEARCHPORT.UPNP.ORG: 1901", lines);
        Assert.Contains("SECURELOCATION.UPNP.ORG: https://192.168.0.10/description.xml", lines);
    }

    [Fact]
    public void ComposeMSearchResponse_WithoutStOrUsn_Throws()
    {
        Assert.Throws<SSDPException>(() => DatagramComposer.ComposeMSearchResponse(new MSearchResponse()));
    }

    [Fact]
    public void ComposeNotify_Alive_IncludesCacheControlLocationAndServer()
    {
        var notify = new Notify
        {
            NTS = NTS.Alive,
            CacheControl = TimeSpan.FromSeconds(1800),
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
            CacheControl = TimeSpan.FromSeconds(1800),
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
    public void ComposeNotify_WithoutUsn_Throws()
    {
        Assert.Throws<SSDPException>(() => DatagramComposer.ComposeNotify(new Notify { NTS = NTS.Alive }));
    }

    [Fact]
    public void ComposedRequest_ParsesBackWithSameValues()
    {
        var request = new MSearchRequest
        {
            TransportType = TransportType.Multicast,
            MX = TimeSpan.FromSeconds(2),
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
}
