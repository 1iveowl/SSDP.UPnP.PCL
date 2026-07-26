using System.Net;
using System.Reactive.Subjects;
using System.Text;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Model;
using Xunit;

namespace SSDP.UPnP.PCL.Tests;

/// <summary>
/// Raw wire capture and the parse-failure diagnostics built on it: the messages
/// the ordinary streams drop in silence, and what the sender actually wrote.
/// </summary>
public class RawCaptureTests
{
    private static HttpRequestResponse Message(
        MessageType messageType,
        Dictionary<string, string> headers,
        string? method = null,
        string? rawText = null) => new()
    {
        MessageType = messageType,
        Method = method,
        Transport = HttpTransport.Udp,
        Headers = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase),
        RawMessage = rawText is null ? default : Encoding.UTF8.GetBytes(rawText),
        LocalEndPoint = new IPEndPoint(IPAddress.Parse("192.168.0.10"), 1900),
        RemoteEndPoint = new IPEndPoint(IPAddress.Parse("192.168.0.20"), 40000)
    };

    private static System.Net.Sockets.UdpClient BindInSearchPortRange()
    {
        for (var attempt = 0; ; attempt++)
        {
            var port = Random.Shared.Next(Constants.MinDynamicPort, Constants.MaxDynamicPort + 1);

            try
            {
                return new System.Net.Sockets.UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            }
            catch (System.Net.Sockets.SocketException) when (attempt < 20)
            {
            }
        }
    }

    private static ControlPoint HotStartedControlPoint(IObservable<HttpRequestResponse> source)
    {
        var controlPoint = new ControlPoint(new ControlPointInterface { IpAddress = IPAddress.Loopback });
        controlPoint.HotStart(source);
        return controlPoint;
    }

    [Fact]
    public void ParseFailures_ReportUnparsableResponses_WithReasonAndBytes()
    {
        const string raw =
            "HTTP/1.1 200 OK\r\n" +
            "Cache-Control: max-age=1800\r\n" +
            "ST: nonsense\r\n" +
            "USN: also-nonsense\r\n" +
            "\r\n";

        var subject = new Subject<HttpRequestResponse>();
        using var controlPoint = HotStartedControlPoint(subject);

        var failures = new List<SsdpParseFailure>();
        var responses = new List<MSearchResponse>();

        using var failureSubscription = controlPoint.ParseFailures().Subscribe(failures.Add);
        using var responseSubscription = controlPoint.MSearchResponseObservable().Subscribe(responses.Add);

        subject.OnNext(Message(MessageType.Response, new Dictionary<string, string>
        {
            ["CACHE-CONTROL"] = "max-age=1800",
            ["ST"] = "nonsense",
            ["USN"] = "also-nonsense"
        }, rawText: raw));

        // Dropped from the ordinary stream, visible on the diagnostic one.
        Assert.Empty(responses);

        var failure = Assert.Single(failures);
        Assert.Contains("Neither ST nor USN", failure.Error);
        Assert.Equal(MessageType.Response, failure.MessageType);
        Assert.Equal(new IPEndPoint(IPAddress.Parse("192.168.0.20"), 40000), failure.RemoteIpEndPoint);
        Assert.Equal(raw, failure.RawMessageText());
    }

    [Fact]
    public void ParseFailures_IgnoreMessagesThatAreNotTheControlPointsBusiness()
    {
        var subject = new Subject<HttpRequestResponse>();
        using var controlPoint = HotStartedControlPoint(subject);

        var failures = new List<SsdpParseFailure>();
        using var subscription = controlPoint.ParseFailures().Subscribe(failures.Add);

        // Another control point's search: not addressed to us, not a failure.
        subject.OnNext(Message(MessageType.Request, new Dictionary<string, string>
        {
            ["HOST"] = "239.255.255.250:1900",
            ["MAN"] = "\"ssdp:discover\"",
            ["ST"] = "ssdp:all"
        }, method: "M-SEARCH"));

        // A well-formed notification: also not a failure.
        subject.OnNext(Message(MessageType.Request, new Dictionary<string, string>
        {
            ["NT"] = "upnp:rootdevice",
            ["NTS"] = "ssdp:alive",
            ["USN"] = "uuid:device-1::upnp:rootdevice"
        }, method: "NOTIFY"));

        Assert.Empty(failures);
    }

    [Fact]
    public void RawMessage_IsEmpty_WhenCaptureIsOff()
    {
        var subject = new Subject<HttpRequestResponse>();
        using var controlPoint = HotStartedControlPoint(subject);

        var received = new List<Notify>();
        using var subscription = controlPoint.NotifyObservable().Subscribe(received.Add);

        // No RawMessage on the incoming message models capture being disabled.
        subject.OnNext(Message(MessageType.Request, new Dictionary<string, string>
        {
            ["NT"] = "upnp:rootdevice",
            ["NTS"] = "ssdp:alive",
            ["USN"] = "uuid:device-1::upnp:rootdevice"
        }, method: "NOTIFY"));

        Assert.True(Assert.Single(received).RawMessage.IsEmpty);
        Assert.False(controlPoint.CaptureRawMessages);
    }

    [Fact]
    public void RawMessage_PreservesWhatTheSenderWrote()
    {
        // Header casing and field order survive here, where the parsed Headers
        // dictionary normalizes them away.
        const string raw =
            "NOTIFY * HTTP/1.1\r\n" +
            "Host: 239.255.255.250:1900\r\n" +
            "nts: ssdp:alive\r\n" +
            "NT: upnp:rootdevice\r\n" +
            "USN: uuid:device-1::upnp:rootdevice\r\n" +
            "\r\n";

        var subject = new Subject<HttpRequestResponse>();
        using var controlPoint = HotStartedControlPoint(subject);

        var received = new List<Notify>();
        using var subscription = controlPoint.NotifyObservable().Subscribe(received.Add);

        subject.OnNext(Message(MessageType.Request, new Dictionary<string, string>
        {
            ["HOST"] = "239.255.255.250:1900",
            ["NTS"] = "ssdp:alive",
            ["NT"] = "upnp:rootdevice",
            ["USN"] = "uuid:device-1::upnp:rootdevice"
        }, method: "NOTIFY", rawText: raw));

        var notify = Assert.Single(received);
        var text = Encoding.UTF8.GetString(notify.RawMessage.Span);

        Assert.Equal(raw, text);
        Assert.Contains("nts: ssdp:alive", text);
    }

    [Fact]
    public async Task Device_ReportsTheSearchesItMustDiscardSilently()
    {
        // UDA 2.0 section 1.3.3 requires the device to ignore a malformed search
        // without replying, which is invisible without this stream.
        // The unicast port must be 1900 or in the SEARCHPORT range, so bind in range.
        var multicastClient = BindInSearchPortRange();

        var rootInterface = new RootDeviceInterface
        {
            RootDeviceConfiguration = new RootDeviceConfiguration
            {
                DeviceUUID = "root-uuid",
                TypeName = "TestRootDevice",
                Version = 1,
                BOOTID = 1,
                IpEndPoint = (IPEndPoint)multicastClient.Client.LocalEndPoint!
            },
            UdpMulticastClient = multicastClient,
            UdpUnicastClient = multicastClient
        };

        var subject = new Subject<HttpRequestResponse>();
        using var device = new Device(rootInterface) { AutoReAdvertise = false };

        var failures = new List<SsdpParseFailure>();
        using var subscription = device.ParseFailureObservable.Subscribe(failures.Add);

        await device.HotStartAsync(subject, skipAlive: true);

        // Multicast search with no MX: must be discarded, and reported here.
        subject.OnNext(Message(MessageType.Request, new Dictionary<string, string>
        {
            ["HOST"] = "239.255.255.250:1900",
            ["MAN"] = "\"ssdp:discover\"",
            ["ST"] = "ssdp:all"
        }, method: "M-SEARCH", rawText: "M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\n\r\n"));

        var failure = Assert.Single(failures);
        Assert.Contains("MX", failure.Error);
        Assert.Equal("M-SEARCH", failure.Method);
        Assert.StartsWith("M-SEARCH * HTTP/1.1", failure.RawMessageText());

        multicastClient.Dispose();
    }

    [Fact]
    public void RawMessageText_IsEmpty_WhenNothingWasCaptured()
    {
        var failure = new SsdpParseFailure { Error = "some error" };

        Assert.Equal(string.Empty, failure.RawMessageText());
    }
}
