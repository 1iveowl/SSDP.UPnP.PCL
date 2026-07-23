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
            UpnpMajorVersion = "2",
            UpnpMinorVersion = "0",
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
        var unicastClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

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

    private static HttpRequestResponse MSearchMessage(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string st) => new()
    {
        MessageType = MessageType.Request,
        Method = "M-SEARCH",
        Transport = HttpTransport.Udp,
        Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HOST"] = "239.255.255.250:1900",
            ["MAN"] = "\"ssdp:discover\"",
            ["MX"] = "0",
            ["ST"] = st,
            ["CPFN.UPNP.ORG"] = "Test CP"
        },
        LocalEndPoint = localEndPoint,
        RemoteEndPoint = remoteEndPoint
    };

    private static async Task<string> ReceiveTextAsync(UdpClient receiver, CancellationToken ct) =>
        Encoding.UTF8.GetString((await receiver.ReceiveAsync(ct)).Buffer);

    private static string HeaderValue(string datagram, string name) =>
        datagram.Split("\r\n").First(line => line.StartsWith($"{name}: ", StringComparison.OrdinalIgnoreCase))[(name.Length + 2)..];

    [Fact]
    public async Task Device_AnswersRootDeviceSearch_WithUnicastResponse()
    {
        var rootInterface = LoopbackInterface(Configuration());
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface);

        await device.HotStartAsync(subject, skipAlive: true);
        Assert.True(device.IsStarted);

        var deviceEndPoint = (IPEndPoint)rootInterface.UdpUnicastClient.Client.LocalEndPoint!;
        var receiverEndPoint = (IPEndPoint)receiver.Client.LocalEndPoint!;

        subject.OnNext(MSearchMessage(deviceEndPoint, receiverEndPoint, "upnp:rootdevice"));

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
    public async Task Device_AnswersSsdpAll_WithFullAdvertisementMatrix()
    {
        var rootInterface = LoopbackInterface(Configuration());
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface);

        var activities = new List<DeviceActivity>();
        using var activitySubscription = device.DeviceActivityObservable.Subscribe(activities.Add);

        await device.HotStartAsync(subject, skipAlive: true);

        var deviceEndPoint = (IPEndPoint)rootInterface.UdpUnicastClient.Client.LocalEndPoint!;
        var receiverEndPoint = (IPEndPoint)receiver.Client.LocalEndPoint!;

        subject.OnNext(MSearchMessage(deviceEndPoint, receiverEndPoint, "ssdp:all"));

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
    public async Task Device_SurvivesFailedSend_AndAnswersNextRequest()
    {
        var rootInterface = LoopbackInterface(Configuration());
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface);

        await device.HotStartAsync(subject, skipAlive: true);

        var deviceEndPoint = (IPEndPoint)rootInterface.UdpUnicastClient.Client.LocalEndPoint!;
        var receiverEndPoint = (IPEndPoint)receiver.Client.LocalEndPoint!;

        // An IPv6 target on an IPv4 socket makes the send itself fail; the
        // pipeline must survive and answer the next request (regression: G1).
        var unreachable = new IPEndPoint(IPAddress.IPv6Loopback, 9999);

        subject.OnNext(MSearchMessage(deviceEndPoint, unreachable, "upnp:rootdevice"));
        subject.OnNext(MSearchMessage(deviceEndPoint, receiverEndPoint, "upnp:rootdevice"));

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
        var receiverEndPoint = (IPEndPoint)receiver.Client.LocalEndPoint!;

        subject.OnNext(MSearchMessage(otherLocalEndPoint, receiverEndPoint, "upnp:rootdevice"));

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

        var deviceEndPoint = (IPEndPoint)rootInterface.UdpUnicastClient.Client.LocalEndPoint!;
        var receiverEndPoint = (IPEndPoint)receiver.Client.LocalEndPoint!;

        subject.OnNext(MSearchMessage(deviceEndPoint, receiverEndPoint, "garbage-st"));
        subject.OnNext(MSearchMessage(deviceEndPoint, receiverEndPoint, "upnp:rootdevice"));

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
        using var device = new Device(rootInterface) { TimeProvider = new FakeTimeProvider(startTime) };

        await device.HotStartAsync(subject, skipAlive: true);

        var deviceEndPoint = (IPEndPoint)rootInterface.UdpUnicastClient.Client.LocalEndPoint!;
        var receiverEndPoint = (IPEndPoint)receiver.Client.LocalEndPoint!;

        subject.OnNext(MSearchMessage(deviceEndPoint, receiverEndPoint, "upnp:rootdevice"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var text = await ReceiveTextAsync(receiver, cts.Token);

        Assert.Contains($"BOOTID.UPNP.ORG: {expectedBootId}\r\n", text);

        DisposeInterface(rootInterface);
    }

    [Fact]
    public async Task Device_UpdateAdvancesBootId()
    {
        var rootInterface = LoopbackInterface(Configuration());
        using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface);

        await device.HotStartAsync(subject, skipAlive: true);

        var beforeUpdate = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // The multicast sends are best-effort (they may fail in a sandboxed
        // network); the BOOTID advance must take effect regardless.
        await device.UpdateAsync(TestContext.Current.CancellationToken);

        var deviceEndPoint = (IPEndPoint)rootInterface.UdpUnicastClient.Client.LocalEndPoint!;
        var receiverEndPoint = (IPEndPoint)receiver.Client.LocalEndPoint!;

        subject.OnNext(MSearchMessage(deviceEndPoint, receiverEndPoint, "upnp:rootdevice"));

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

        var deviceEndPoint = (IPEndPoint)rootInterface.UdpUnicastClient.Client.LocalEndPoint!;
        var receiverEndPoint = (IPEndPoint)receiver.Client.LocalEndPoint!;

        var update = device.UpdateAsync(TestContext.Current.CancellationToken);

        for (var i = 0; i < 5; i++)
        {
            subject.OnNext(MSearchMessage(deviceEndPoint, receiverEndPoint, "upnp:rootdevice"));
        }

        await update;

        subject.OnNext(MSearchMessage(deviceEndPoint, receiverEndPoint, "upnp:rootdevice"));

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
    public void Device_WithoutInterfaces_Throws()
    {
        Assert.Throws<SSDPException>(() => new Device(Array.Empty<RootDeviceInterface>()));
        Assert.Throws<SSDPException>(() => new Device(new RootDeviceConfiguration()));
    }
}
