using System.Net;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Model;
using Xunit;

namespace SSDP.UPnP.PCL.Tests;

public class ControlPointTests
{
    private static HttpRequestResponse NotifyMessage(string nts, string usn = "uuid:device-1::upnp:rootdevice") => new()
    {
        MessageType = MessageType.Request,
        Method = "NOTIFY",
        Transport = HttpTransport.Udp,
        Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HOST"] = "239.255.255.250:1900",
            ["NT"] = "upnp:rootdevice",
            ["NTS"] = nts,
            ["USN"] = usn
        }
    };

    private static HttpRequestResponse ResponseMessage() => new()
    {
        MessageType = MessageType.Response,
        StatusCode = 200,
        ReasonPhrase = "OK",
        Transport = HttpTransport.Udp,
        Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CACHE-CONTROL"] = "max-age=1800",
            ["ST"] = "upnp:rootdevice",
            ["USN"] = "uuid:device-1::upnp:rootdevice",
            ["LOCATION"] = "http://192.168.0.20/description.xml"
        },
        RemoteEndPoint = new IPEndPoint(IPAddress.Parse("192.168.0.20"), 1900)
    };

    private static ControlPoint HotStartedControlPoint(IObservable<HttpRequestResponse> source)
    {
        var controlPoint = new ControlPoint(new ControlPointInterface { IpAddress = IPAddress.Loopback });
        controlPoint.HotStart(source);
        return controlPoint;
    }

    [Fact]
    public void Constructors_WithoutArguments_Throw()
    {
        Assert.Throws<SSDPException>(() => new ControlPoint(Array.Empty<IPAddress>()));
        Assert.Throws<SSDPException>(() => new ControlPoint(Array.Empty<ControlPointInterface>()));
    }

    [Fact]
    public void HotStart_Twice_Throws()
    {
        var subject = new Subject<HttpRequestResponse>();
        using var controlPoint = HotStartedControlPoint(subject);

        Assert.Throws<SSDPException>(() => controlPoint.HotStart(subject));
    }

    [Fact]
    public void HotStartedStreams_NeedNoStart()
    {
        // The HotStart seam must keep working with no start step anywhere — this
        // is the path downstream test suites drive from a Subject, without sockets.
        var subject = new Subject<HttpRequestResponse>();
        using var controlPoint = HotStartedControlPoint(subject);

        var received = new List<Notify>();
        using var subscription = controlPoint.NotifyObservable().Subscribe(received.Add);

        subject.OnNext(NotifyMessage("ssdp:alive"));

        Assert.Single(received);
    }

    [Fact]
    public void HotStartedStream_Resubscribes()
    {
        var subject = new Subject<HttpRequestResponse>();
        using var controlPoint = HotStartedControlPoint(subject);

        var first = new List<Notify>();
        var subscription = controlPoint.NotifyObservable().Subscribe(first.Add);

        subject.OnNext(NotifyMessage("ssdp:alive"));
        subscription.Dispose();

        var second = new List<Notify>();
        using var resubscription = controlPoint.NotifyObservable().Subscribe(second.Add);

        subject.OnNext(NotifyMessage("ssdp:byebye"));

        Assert.Equal(NTS.Alive, Assert.Single(first).NTS);
        Assert.Equal(NTS.ByeBye, Assert.Single(second).NTS);
    }

    [Fact]
    public void ParsedStreams_AreSharedAcrossCalls()
    {
        var subject = new Subject<HttpRequestResponse>();
        using var controlPoint = HotStartedControlPoint(subject);

        var firstStream = new List<Notify>();
        var secondStream = new List<Notify>();

        using var first = controlPoint.NotifyObservable().Subscribe(firstStream.Add);
        using var second = controlPoint.NotifyObservable().Subscribe(secondStream.Add);

        subject.OnNext(NotifyMessage("ssdp:alive"));

        var a = Assert.Single(firstStream);
        var b = Assert.Single(secondStream);

        // One parse, one record instance, delivered to both subscribers.
        Assert.Same(a, b);
    }

    [Fact]
    public void NotifyObservable_EmitsAliveByeByeAndUpdate()
    {
        var subject = new Subject<HttpRequestResponse>();
        using var controlPoint = HotStartedControlPoint(subject);

        var received = new List<Notify>();
        using var subscription = controlPoint.NotifyObservable().Subscribe(received.Add);

        subject.OnNext(NotifyMessage("ssdp:alive"));
        subject.OnNext(NotifyMessage("ssdp:byebye"));
        subject.OnNext(NotifyMessage("ssdp:update"));

        Assert.Equal([NTS.Alive, NTS.ByeBye, NTS.Update], received.Select(notify => notify.NTS));
    }

    [Fact]
    public void NotifyObservable_DropsUnknownNtsAndNonNotifyMessages()
    {
        var subject = new Subject<HttpRequestResponse>();
        using var controlPoint = HotStartedControlPoint(subject);

        var received = new List<Notify>();
        using var subscription = controlPoint.NotifyObservable().Subscribe(received.Add);

        subject.OnNext(NotifyMessage("upnp:propchange"));
        subject.OnNext(NotifyMessage("nonsense"));
        subject.OnNext(ResponseMessage());
        subject.OnNext(NotifyMessage("ssdp:alive") with { Method = "M-SEARCH" });

        Assert.Empty(received);
    }

    [Fact]
    public void MSearchResponseObservable_EmitsParsedResponses()
    {
        var subject = new Subject<HttpRequestResponse>();
        using var controlPoint = HotStartedControlPoint(subject);

        var received = new List<MSearchResponse>();
        using var subscription = controlPoint.MSearchResponseObservable().Subscribe(received.Add);

        subject.OnNext(ResponseMessage());
        subject.OnNext(NotifyMessage("ssdp:alive"));

        var response = Assert.Single(received);
        Assert.Equal(TimeSpan.FromSeconds(1800), response.MaxAge);
        Assert.Equal("device-1", response.USN?.DeviceUUID);
        Assert.Equal(STType.RootDeviceSearch, response.ST?.StSearchType);
    }

    [Fact]
    public async Task SendMSearchAsync_UnknownAddress_Throws()
    {
        var subject = new Subject<HttpRequestResponse>();
        using var controlPoint = HotStartedControlPoint(subject);

        var request = new MSearchRequest { ST = new ST { StSearchType = STType.All } };

        await Assert.ThrowsAsync<SSDPException>(
            () => controlPoint.SendMSearchAsync(request, IPAddress.Parse("10.99.99.99"), TestContext.Current.CancellationToken));
    }
}
