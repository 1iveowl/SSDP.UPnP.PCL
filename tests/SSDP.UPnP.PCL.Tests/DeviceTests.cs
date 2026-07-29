using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Model;
using Xunit;

namespace SSDP.UPnP.PCL.Tests;

public class DeviceTests
{
    private static RootDeviceConfiguration Configuration(uint bootId = 123) => new()
    {
        DeviceUUID = "root-uuid",
        TypeName = "TestRootDevice",
        Version = 1,
        BOOTID = bootId,
        CONFIGID = 5,
        CacheControl = TimeSpan.FromSeconds(1800),
        Location = new Uri("http://127.0.0.1/description.xml"),
        Server = new Server
        {
            OperatingSystem = "Linux",
            OperatingSystemVersion = "6.1",
            UpnpMajorVersion = 2,
            UpnpMinorVersion = 0,
            ProductName = "Test",
            ProductVersion = "1.0"
        },
        Services =
        [
            new ServiceConfiguration { TypeName = "TestService", Version = 1 }
        ]
    };

    private static RootDeviceInterface LoopbackInterface(RootDeviceConfiguration configuration)
    {
        var multicastClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var unicastClient = LoopbackSockets.Udp();

        return new RootDeviceInterface
        {
            RootDeviceConfiguration = configuration,
            UdpMulticastClient = multicastClient,
            UdpUnicastClient = unicastClient
        };
    }

    private static void DisposeInterface(RootDeviceInterface rootInterface)
    {
        rootInterface.UdpMulticastClient.Dispose();

        if (rootInterface.UdpUnicastClient != rootInterface.UdpMulticastClient)
        {
            rootInterface.UdpUnicastClient.Dispose();
        }
    }

    // A unicast M-SEARCH (HOST names the device, no MX) — answered immediately.
    private static HttpRequestResponse UnicastMSearch(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string st) => new()
    {
        MessageType = MessageType.Request,
        Method = "M-SEARCH",
        Transport = HttpTransport.Udp,
        Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HOST"] = localEndPoint.ToString(),
            ["MAN"] = "\"ssdp:discover\"",
            ["ST"] = st
        },
        LocalEndPoint = localEndPoint,
        RemoteEndPoint = remoteEndPoint
    };

    private static HttpRequestResponse MulticastMSearch(
        IPEndPoint localEndPoint,
        IPEndPoint remoteEndPoint,
        string st,
        string? mx = "1",
        string man = "\"ssdp:discover\"",
        int? tcpPort = null)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HOST"] = "239.255.255.250:1900",
            ["MAN"] = man,
            ["ST"] = st,
            ["CPFN.UPNP.ORG"] = "Test CP"
        };

        if (mx is not null)
        {
            headers["MX"] = mx;
        }

        if (tcpPort is not null)
        {
            headers["TCPPORT.UPNP.ORG"] = tcpPort.Value.ToString();
        }

        return new HttpRequestResponse
        {
            MessageType = MessageType.Request,
            Method = "M-SEARCH",
            Transport = HttpTransport.Udp,
            Headers = headers,
            LocalEndPoint = localEndPoint,
            RemoteEndPoint = remoteEndPoint
        };
    }

    private static async Task<string> ReceiveTextAsync(UdpClient receiver, CancellationToken ct) =>
        Encoding.UTF8.GetString((await receiver.ReceiveAsync(ct)).Buffer);

    private static string HeaderValue(string datagram, string name) =>
        datagram.Split("\r\n").First(line => line.StartsWith($"{name}: ", StringComparison.OrdinalIgnoreCase))[(name.Length + 2)..];

    private static IPEndPoint DeviceEndPoint(RootDeviceInterface rootInterface) =>
        (IPEndPoint)rootInterface.UdpUnicastClient.Client.LocalEndPoint!;

    private static IPEndPoint ReceiverEndPoint(UdpClient receiver) =>
        (IPEndPoint)receiver.Client.LocalEndPoint!;

    [Fact]
    public async Task Device_AnswersRootDeviceSearch_WithUnicastResponse()
    {
        var rootInterface = LoopbackInterface(Configuration());
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface);

        await device.HotStartAsync(subject, skipAlive: true);
        Assert.True(device.IsStarted);

        subject.OnNext(UnicastMSearch(DeviceEndPoint(rootInterface), ReceiverEndPoint(receiver), "upnp:rootdevice"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var text = await ReceiveTextAsync(receiver, cts.Token);

        Assert.StartsWith("HTTP/1.1 200 OK\r\n", text);
        Assert.Contains("ST: upnp:rootdevice\r\n", text);
        Assert.Contains("USN: uuid:root-uuid::upnp:rootdevice\r\n", text);
        Assert.Contains("BOOTID.UPNP.ORG: 123\r\n", text);
        Assert.Contains("CONFIGID.UPNP.ORG: 5\r\n", text);
        Assert.Contains("CACHE-CONTROL: max-age=1800\r\n", text);
        Assert.EndsWith("\r\n\r\n", text);

        DisposeInterface(rootInterface);
    }

    [Fact]
    public async Task Device_AnswersValidMulticastSearch()
    {
        var rootInterface = LoopbackInterface(Configuration());
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface);

        await device.HotStartAsync(subject, skipAlive: true);

        subject.OnNext(MulticastMSearch(DeviceEndPoint(rootInterface), ReceiverEndPoint(receiver), "upnp:rootdevice", mx: "1"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var text = await ReceiveTextAsync(receiver, cts.Token);

        Assert.Contains("USN: uuid:root-uuid::upnp:rootdevice\r\n", text);

        DisposeInterface(rootInterface);
    }

    [Theory]
    [InlineData(null, "\"ssdp:discover\"")] // missing MX
    [InlineData("0", "\"ssdp:discover\"")]  // MX below 1
    [InlineData("1", "\"ssdp:wrong\"")]     // invalid MAN
    [InlineData("1", "")]                   // missing MAN value
    public async Task Device_SilentlyDiscardsInvalidMulticastSearches(string? mx, string man)
    {
        var rootInterface = LoopbackInterface(Configuration());
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface);

        await device.HotStartAsync(subject, skipAlive: true);

        subject.OnNext(MulticastMSearch(DeviceEndPoint(rootInterface), ReceiverEndPoint(receiver), "upnp:rootdevice", mx, man));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await receiver.ReceiveAsync(cts.Token));

        DisposeInterface(rootInterface);
    }

    [Fact]
    public async Task Device_AnswersSsdpAll_WithFullAdvertisementMatrix()
    {
        var rootInterface = LoopbackInterface(Configuration());
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface);

        var activities = new List<DeviceActivity>();
        using var activitySubscription = device.DeviceActivityObservable.Subscribe(activities.Add);

        await device.HotStartAsync(subject, skipAlive: true);

        subject.OnNext(UnicastMSearch(DeviceEndPoint(rootInterface), ReceiverEndPoint(receiver), "ssdp:all"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Root device (3 messages) + one service = four responses, in any order
        // (responses are sent concurrently).
        var usns = new List<string>();

        for (var i = 0; i < 4; i++)
        {
            usns.Add(HeaderValue(await ReceiveTextAsync(receiver, cts.Token), "USN"));
        }

        Assert.Equal(
        [
            "uuid:root-uuid",
            "uuid:root-uuid::upnp:rootdevice",
            "uuid:root-uuid::urn:schemas-upnp-org:device:TestRootDevice:1",
            "uuid:root-uuid::urn:schemas-upnp-org:service:TestService:1"
        ], usns.OrderBy(usn => usn, StringComparer.Ordinal).ToList());

        Assert.Contains(DeviceActivity.Responding, activities);

        DisposeInterface(rootInterface);
    }

    [Fact]
    public async Task Device_EchoesRequestedVersionInResponseSt()
    {
        // Device supports version 1; a version 1 search gets ST version 1 back even
        // though the USN carries the advertised identity. Search version equals
        // entity version here, so also assert the distinct-versions case at the
        // matcher level (SearchMatcherTests); this test pins the wire format.
        var rootInterface = LoopbackInterface(Configuration());
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface);

        await device.HotStartAsync(subject, skipAlive: true);

        subject.OnNext(UnicastMSearch(
            DeviceEndPoint(rootInterface),
            ReceiverEndPoint(receiver),
            "urn:schemas-upnp-org:service:TestService:1"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var text = await ReceiveTextAsync(receiver, cts.Token);

        Assert.Contains("ST: urn:schemas-upnp-org:service:TestService:1\r\n", text);
        Assert.Contains("USN: uuid:root-uuid::urn:schemas-upnp-org:service:TestService:1\r\n", text);

        DisposeInterface(rootInterface);
    }

    [Fact]
    public async Task Device_RepliesOverTcp_WhenTcpPortRequested()
    {
        var rootInterface = LoopbackInterface(Configuration());
        using var tcpListener = LoopbackSockets.Tcp();

        var tcpPort = ((IPEndPoint)tcpListener.LocalEndpoint).Port;

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface);

        await device.HotStartAsync(subject, skipAlive: true);

        var requesterEndPoint = new IPEndPoint(IPAddress.Loopback, 41000);

        subject.OnNext(MulticastMSearch(
            DeviceEndPoint(rootInterface), requesterEndPoint, "ssdp:all", mx: "1", tcpPort: tcpPort));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var connection = await tcpListener.AcceptTcpClientAsync(cts.Token);
        using var reader = new StreamReader(connection.GetStream(), Encoding.UTF8);

        var text = await reader.ReadToEndAsync(cts.Token);

        // All four responses arrive on one reliable connection, without MX delays.
        Assert.Contains("USN: uuid:root-uuid::upnp:rootdevice\r\n", text);
        Assert.Contains("USN: uuid:root-uuid\r\n", text);
        Assert.Contains("USN: uuid:root-uuid::urn:schemas-upnp-org:device:TestRootDevice:1\r\n", text);
        Assert.Contains("USN: uuid:root-uuid::urn:schemas-upnp-org:service:TestService:1\r\n", text);

        tcpListener.Stop();
        DisposeInterface(rootInterface);
    }

    [Fact]
    public async Task Device_SurvivesFailedSend_AndAnswersNextRequest()
    {
        var rootInterface = LoopbackInterface(Configuration());
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface);

        await device.HotStartAsync(subject, skipAlive: true);

        // An IPv6 target on an IPv4 socket makes the send itself fail; the
        // pipeline must survive and answer the next request (regression: G1).
        var unreachable = new IPEndPoint(IPAddress.IPv6Loopback, 9999);

        subject.OnNext(UnicastMSearch(DeviceEndPoint(rootInterface), unreachable, "upnp:rootdevice"));
        subject.OnNext(UnicastMSearch(DeviceEndPoint(rootInterface), ReceiverEndPoint(receiver), "upnp:rootdevice"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var text = await ReceiveTextAsync(receiver, cts.Token);

        Assert.Contains("USN: uuid:root-uuid::upnp:rootdevice\r\n", text);

        DisposeInterface(rootInterface);
    }

    [Fact]
    public async Task Device_IgnoresSearchesForOtherInterfaces()
    {
        var rootInterface = LoopbackInterface(Configuration());
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface);

        await device.HotStartAsync(subject, skipAlive: true);

        var otherLocalEndPoint = new IPEndPoint(IPAddress.Parse("10.1.2.3"), 1900);

        subject.OnNext(UnicastMSearch(otherLocalEndPoint, ReceiverEndPoint(receiver), "upnp:rootdevice"));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await receiver.ReceiveAsync(cts.Token));

        DisposeInterface(rootInterface);
    }

    [Fact]
    public async Task Device_DropsUnparsableSearches_AndKeepsRunning()
    {
        var rootInterface = LoopbackInterface(Configuration());
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface);

        await device.HotStartAsync(subject, skipAlive: true);

        subject.OnNext(UnicastMSearch(DeviceEndPoint(rootInterface), ReceiverEndPoint(receiver), "garbage-st"));
        subject.OnNext(UnicastMSearch(DeviceEndPoint(rootInterface), ReceiverEndPoint(receiver), "upnp:rootdevice"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var text = await ReceiveTextAsync(receiver, cts.Token);

        Assert.Contains("ST: upnp:rootdevice\r\n", text);

        DisposeInterface(rootInterface);
    }

    [Fact]
    public async Task Device_StampsUnsetBootIdsAtStart_FromTimeProvider()
    {
        var startTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var expectedBootId = (uint)startTime.ToUnixTimeSeconds();

        var rootInterface = LoopbackInterface(Configuration(bootId: 0));
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface)
        {
            TimeProvider = new FakeTimeProvider(startTime),
            AutoReAdvertise = false
        };

        await device.HotStartAsync(subject, skipAlive: true);

        subject.OnNext(UnicastMSearch(DeviceEndPoint(rootInterface), ReceiverEndPoint(receiver), "upnp:rootdevice"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var text = await ReceiveTextAsync(receiver, cts.Token);

        Assert.Contains($"BOOTID.UPNP.ORG: {expectedBootId}\r\n", text);

        DisposeInterface(rootInterface);
    }

    [Fact]
    public async Task Device_ReAdvertisesBeforeExpiry()
    {
        var startTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(startTime);

        var rootInterface = LoopbackInterface(Configuration());

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface) { TimeProvider = fakeTime };

        var activities = new List<DeviceActivity>();
        using var activitySubscription = device.DeviceActivityObservable.Subscribe(activities.Add);

        // skipAlive suppresses the initial burst; the re-advertise loop must still
        // fire within max-age/2 (900s here) of fake time.
        await device.HotStartAsync(subject, skipAlive: true);

        for (var i = 0; i < 60 && !activities.Contains(DeviceActivity.Notifying); i++)
        {
            fakeTime.Advance(TimeSpan.FromSeconds(30));
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Contains(DeviceActivity.Notifying, activities);

        DisposeInterface(rootInterface);
    }

    [Fact]
    public async Task Device_UpdateAdvancesBootId_AndReAdvertises()
    {
        var rootInterface = LoopbackInterface(Configuration());
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface);

        var activities = new List<DeviceActivity>();
        using var activitySubscription = device.DeviceActivityObservable.Subscribe(activities.Add);

        await device.HotStartAsync(subject, skipAlive: true);

        var beforeUpdate = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // The multicast sends are best-effort (they may fail in a sandboxed
        // network); the BOOTID advance must take effect regardless, and the update
        // set must be followed by an alive set (two Notifying batches).
        await device.UpdateAsync(TestContext.Current.CancellationToken);

        Assert.True(activities.Count(activity => activity == DeviceActivity.Notifying) >= 2);

        subject.OnNext(UnicastMSearch(DeviceEndPoint(rootInterface), ReceiverEndPoint(receiver), "upnp:rootdevice"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var bootId = uint.Parse(HeaderValue(await ReceiveTextAsync(receiver, cts.Token), "BOOTID.UPNP.ORG"));

        Assert.InRange(bootId, beforeUpdate, beforeUpdate + 60);

        DisposeInterface(rootInterface);
    }

    [Fact]
    public async Task Device_AnswersSearches_WhileUpdating()
    {
        var rootInterface = LoopbackInterface(Configuration());
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface);

        await device.HotStartAsync(subject, skipAlive: true);

        var update = device.UpdateAsync(TestContext.Current.CancellationToken);

        for (var i = 0; i < 5; i++)
        {
            subject.OnNext(UnicastMSearch(DeviceEndPoint(rootInterface), ReceiverEndPoint(receiver), "upnp:rootdevice"));
        }

        await update;

        subject.OnNext(UnicastMSearch(DeviceEndPoint(rootInterface), ReceiverEndPoint(receiver), "upnp:rootdevice"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        for (var i = 0; i < 6; i++)
        {
            var text = await ReceiveTextAsync(receiver, cts.Token);
            Assert.Contains("USN: uuid:root-uuid::upnp:rootdevice\r\n", text);
        }

        DisposeInterface(rootInterface);
    }

    [Fact]
    public async Task Device_CannotBeStartedTwice()
    {
        var rootInterface = LoopbackInterface(Configuration());

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface);

        await device.HotStartAsync(subject, skipAlive: true);

        await Assert.ThrowsAsync<SSDPException>(() => device.HotStartAsync(subject, skipAlive: true));

        DisposeInterface(rootInterface);
    }

    [Fact]
    public async Task Device_AnswersSearch_OnWildcardBoundSocket()
    {
        // Multicast is received on a wildcard-bound socket on Linux/macOS, and the
        // listener reports the interface the datagram actually arrived on (not the
        // socket's 0.0.0.0 bind address). Interface matching must accept that, or
        // the device silently ignores every multicast M-SEARCH.
        var multicastClient = LoopbackSockets.WildcardUdp();
        var boundPort = ((IPEndPoint)multicastClient.Client.LocalEndPoint!).Port;

        var configuration = Configuration() with
        {
            IpEndPoint = new IPEndPoint(IPAddress.Loopback, boundPort)
        };

        var rootInterface = new RootDeviceInterface
        {
            RootDeviceConfiguration = configuration,
            UdpMulticastClient = multicastClient,
            UdpUnicastClient = multicastClient
        };

        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface) { AutoReAdvertise = false };

        await device.HotStartAsync(subject, skipAlive: true);

        // The datagram arrived on the loopback interface, port as bound.
        var arrivedOn = new IPEndPoint(IPAddress.Loopback, boundPort);

        subject.OnNext(MulticastMSearch(arrivedOn, ReceiverEndPoint(receiver), "upnp:rootdevice", mx: "1"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var text = await ReceiveTextAsync(receiver, cts.Token);

        Assert.Contains("USN: uuid:root-uuid::upnp:rootdevice\r\n", text);

        multicastClient.Dispose();
    }

    [Fact]
    public void SearchPort_IsNullOnTheDefaultSsdpPort()
    {
        // UDA 2.0 section 1.2.2: a device answering on 1900 does not advertise
        // SEARCHPORT.UPNP.ORG at all.
        using var onDefaultPort = new UdpClient(new IPEndPoint(IPAddress.Loopback, Constants.UdpSSDPMulticastPort));

        var defaultPortInterface = new RootDeviceInterface
        {
            RootDeviceConfiguration = Configuration(),
            UdpMulticastClient = onDefaultPort,
            UdpUnicastClient = onDefaultPort
        };

        Assert.Null(defaultPortInterface.SearchPort);

        var rootInterface = LoopbackInterface(Configuration());
        var boundPort = ((IPEndPoint)rootInterface.UdpUnicastClient.Client.LocalEndPoint!).Port;

        Assert.Equal(boundPort, rootInterface.SearchPort?.Port);

        DisposeInterface(rootInterface);
    }

    [Fact]
    public void IsMatchingInterface_WildcardBound_RequiresMatchingConfiguredAddress()
    {
        var multicastClient = LoopbackSockets.WildcardUdp();
        var boundPort = ((IPEndPoint)multicastClient.Client.LocalEndPoint!).Port;

        var rootInterface = new RootDeviceInterface
        {
            RootDeviceConfiguration = Configuration() with
            {
                IpEndPoint = new IPEndPoint(IPAddress.Loopback, boundPort)
            },
            UdpMulticastClient = multicastClient,
            UdpUnicastClient = multicastClient
        };

        // The configured interface address, on the bound port: ours.
        Assert.True(rootInterface.IsMatchingInterface(new IPEndPoint(IPAddress.Loopback, boundPort)));

        // A different interface, or a different port: not ours.
        Assert.False(rootInterface.IsMatchingInterface(new IPEndPoint(IPAddress.Parse("10.1.2.3"), boundPort)));
        Assert.False(rootInterface.IsMatchingInterface(new IPEndPoint(IPAddress.Loopback, boundPort + 1)));
        Assert.False(rootInterface.IsMatchingInterface(null));

        multicastClient.Dispose();
    }

    [Fact]
    public async Task DisposeAsync_SendsByeByeThenReleases()
    {
        var rootInterface = LoopbackInterface(Configuration());

        var subject = new Subject<HttpRequestResponse>();
        var device = new Device(rootInterface) { AutoReAdvertise = false };

        var activities = new List<DeviceActivity>();
        var completed = false;

        using var activitySubscription = device.DeviceActivityObservable
            .Subscribe(activities.Add, () => completed = true);

        await device.HotStartAsync(subject, skipAlive: true);

        await device.DisposeAsync();

        // The goodbye was attempted before teardown, and the device released
        // afterwards - the activity stream completes as part of disposal.
        Assert.Contains(DeviceActivity.Notifying, activities);
        Assert.True(completed);

        DisposeInterface(rootInterface);
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent_AndComposesWithDispose()
    {
        var rootInterface = LoopbackInterface(Configuration());

        var subject = new Subject<HttpRequestResponse>();
        var device = new Device(rootInterface) { AutoReAdvertise = false };

        await device.HotStartAsync(subject, skipAlive: true);

        await device.DisposeAsync();
        await device.DisposeAsync();
        device.Dispose();

        DisposeInterface(rootInterface);
    }

    [Fact]
    public void Dispose_NeverStarted_AndTwice_IsSafe()
    {
        var rootInterface = LoopbackInterface(Configuration());

        var device = new Device(rootInterface);

        device.Dispose();
        device.Dispose();

        DisposeInterface(rootInterface);
    }

    [Fact]
    public async Task DisposeAsync_NeverStarted_IsSafe()
    {
        var rootInterface = LoopbackInterface(Configuration());

        var device = new Device(rootInterface) { AutoReAdvertise = false };

        await device.DisposeAsync();

        DisposeInterface(rootInterface);
    }

    [Fact]
    public async Task DisposeAsync_BoundsTheGoodbye_WhenSendingCannotComplete()
    {
        // A device whose sockets are already gone cannot say goodbye; disposal must
        // still complete rather than hang.
        var rootInterface = LoopbackInterface(Configuration());

        var subject = new Subject<HttpRequestResponse>();
        var device = new Device(rootInterface)
        {
            AutoReAdvertise = false,
            ByeByeTimeout = TimeSpan.FromMilliseconds(200)
        };

        await device.HotStartAsync(subject, skipAlive: true);

        DisposeInterface(rootInterface);

        await device.DisposeAsync();
    }

    [Fact]
    public void Device_WithoutInterfaces_Throws()
    {
        Assert.Throws<SSDPException>(() => new Device(Array.Empty<RootDeviceInterface>()));
        Assert.Throws<SSDPException>(() => new Device(
            new RootDeviceConfiguration { Location = new Uri("http://127.0.0.1/description.xml") }));
    }

    // A service that cannot form a URI would otherwise fail once per message at
    // send time, where the failure is logged and the device quietly advertises
    // less than it should.
    // The endpoint has to be set, or construction throws "must be fully specified"
    // before it ever looks at the services - and the assertion below would pass
    // with the service validation deleted. The message is asserted for the same
    // reason: SSDPException on its own says nothing about which check ran.
    private static RootDeviceConfiguration ConfigurationWithServices(params ServiceConfiguration[] services) =>
        Configuration() with
        {
            IpEndPoint = new IPEndPoint(IPAddress.Loopback, Constants.UdpSSDPMulticastPort),
            Services = services
        };

    [Fact]
    public void Device_RejectsAServiceWithoutATypeName()
    {
        var error = Assert.Throws<SSDPException>(
            () => new Device(ConfigurationWithServices(new ServiceConfiguration { Version = 1 })));

        Assert.Contains("TypeName", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Device_RejectsADeviceTypeWithoutAVersion()
    {
        var configuration = ConfigurationWithServices() with { Version = 0 };

        var error = Assert.Throws<SSDPException>(() => new Device(configuration));

        Assert.Contains("version", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Device_RejectsAServiceWithoutAVersion()
    {
        var error = Assert.Throws<SSDPException>(
            () => new Device(ConfigurationWithServices(new ServiceConfiguration { TypeName = "TestService" })));

        Assert.Contains("version", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Device_ValidatesConfiguration()
    {
        // SEARCHPORT rule: port must be 1900 or 49152-65535.
        Assert.Throws<SSDPException>(() => new Device(
            Configuration() with { IpEndPoint = new IPEndPoint(IPAddress.Loopback, 1901) }));

        // Every device needs a UUID.
        var noUuidInterface = LoopbackInterface(Configuration() with { DeviceUUID = null });
        Assert.Throws<SSDPException>(() => new Device(noUuidInterface));
        DisposeInterface(noUuidInterface);

        // CONFIGID free range is 0-16777215.
        var badConfigIdInterface = LoopbackInterface(Configuration() with { CONFIGID = 16777216 });
        Assert.Throws<SSDPException>(() => new Device(badConfigIdInterface));
        DisposeInterface(badConfigIdInterface);

        // Prepared unicast clients must also honor the SEARCHPORT port rule.
        var lowPortClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var lowPortInterface = new RootDeviceInterface
        {
            RootDeviceConfiguration = Configuration(),
            UdpMulticastClient = lowPortClient,
            UdpUnicastClient = lowPortClient
        };

        if (((IPEndPoint)lowPortClient.Client.LocalEndPoint!).Port is >= Constants.MinDynamicPort and <= Constants.MaxDynamicPort)
        {
            // Rare: the ephemeral port landed in the legal range; nothing to assert.
            lowPortClient.Dispose();
        }
        else
        {
            Assert.Throws<SSDPException>(() => new Device(lowPortInterface));
            lowPortClient.Dispose();
        }
    }
}
