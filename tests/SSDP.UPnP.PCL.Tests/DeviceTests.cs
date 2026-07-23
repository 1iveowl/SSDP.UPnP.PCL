using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Text;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Model;
using Xunit;

namespace SSDP.UPnP.PCL.Tests;

public class DeviceTests
{
    private static RootDeviceConfiguration Configuration() => new()
    {
        EntityType = EntityType.RootDevice,
        DeviceUUID = "root-uuid",
        TypeName = "TestRootDevice",
        Version = 1,
        BOOTID = 123,
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
            new ServiceConfiguration { EntityType = EntityType.ServiceType, TypeName = "TestService", Version = 1 }
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
        var result = await receiver.ReceiveAsync(cts.Token);
        var text = Encoding.UTF8.GetString(result.Buffer);

        Assert.StartsWith("HTTP/1.1 200 OK\r\n", text);
        Assert.Contains("ST: upnp:rootdevice\r\n", text);
        Assert.Contains("USN: uuid:root-uuid::upnp:rootdevice\r\n", text);
        Assert.Contains("BOOTID.UPNP.ORG: 123\r\n", text);
        Assert.Contains("CONFIGID.UPNP.ORG: 5\r\n", text);
        Assert.Contains("CACHE-CONTROL: max-age=1800\r\n", text);
        Assert.EndsWith("\r\n\r\n", text);

        rootInterface.UdpMulticastClient.Dispose();
        rootInterface.UdpUnicastClient.Dispose();
    }

    [Fact]
    public async Task Device_AnswersSsdpAll_OncePerEntity()
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

        // Root device + one service = two responses.
        var first = Encoding.UTF8.GetString((await receiver.ReceiveAsync(cts.Token)).Buffer);
        var second = Encoding.UTF8.GetString((await receiver.ReceiveAsync(cts.Token)).Buffer);

        Assert.Contains("USN: uuid:root-uuid::upnp:rootdevice\r\n", first);
        Assert.Contains("USN: uuid:root-uuid::urn:schemas-upnp-org:service:TestService:1\r\n", second);
        Assert.Contains(DeviceActivity.Responding, activities);

        rootInterface.UdpMulticastClient.Dispose();
        rootInterface.UdpUnicastClient.Dispose();
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

        rootInterface.UdpMulticastClient.Dispose();
        rootInterface.UdpUnicastClient.Dispose();
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

        // Malformed ST must not kill the pipeline (regression: C7).
        subject.OnNext(MSearchMessage(deviceEndPoint, receiverEndPoint, "garbage-st"));
        subject.OnNext(MSearchMessage(deviceEndPoint, receiverEndPoint, "upnp:rootdevice"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var text = Encoding.UTF8.GetString((await receiver.ReceiveAsync(cts.Token)).Buffer);

        Assert.Contains("ST: upnp:rootdevice\r\n", text);

        rootInterface.UdpMulticastClient.Dispose();
        rootInterface.UdpUnicastClient.Dispose();
    }

    [Fact]
    public void Device_WithoutInterfaces_Throws()
    {
        Assert.Throws<SSDPException>(() => new Device(Array.Empty<RootDeviceInterface>()));
        Assert.Throws<SSDPException>(() => new Device(new RootDeviceConfiguration()));
    }
}
