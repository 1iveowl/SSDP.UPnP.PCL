using System.Net;
using System.Net.Sockets;
using System.Text;
using SSDP.UPnP.PCL.Model;
using Xunit;

namespace SSDP.UPnP.PCL.Tests;

/// <summary>
/// The lazy-start lifecycle: nothing binds until the first subscription, the
/// first-subscriber race is safe, and listening restarts after the last
/// subscription is disposed.
/// </summary>
public class ControlPointLifecycleTests
{
    private const string AliveNotify =
        "NOTIFY * HTTP/1.1\r\n" +
        "HOST: 239.255.255.250:1900\r\n" +
        "CACHE-CONTROL: max-age=1800\r\n" +
        "LOCATION: http://192.168.0.20/description.xml\r\n" +
        "NT: upnp:rootdevice\r\n" +
        "NTS: ssdp:alive\r\n" +
        "USN: uuid:device-1::upnp:rootdevice\r\n" +
        "\r\n";

    // A control point over a loopback UDP socket: no multicast, no fixed ports —
    // deterministic in a container and in CI. The factory counts materializations.
    private static (ControlPoint ControlPoint, Func<int> SocketSetupCount, Func<IPEndPoint> EndPoint) CreateLoopbackControlPoint()
    {
        var setupCount = 0;
        UdpClient? udpClient = null;

        var controlPoint = new ControlPoint(
            () =>
            {
                Interlocked.Increment(ref setupCount);

                udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

                return [new ControlPointInterface { IpAddress = IPAddress.Loopback, UdpClient = udpClient }];
            },
            isClientsProvided: false);

        return (controlPoint,
            () => Volatile.Read(ref setupCount),
            () => (IPEndPoint)udpClient!.Client.LocalEndPoint!);
    }

    private static async Task SendAliveAsync(IPEndPoint target, CancellationToken ct)
    {
        using var sender = new UdpClient();
        var datagram = Encoding.UTF8.GetBytes(AliveNotify);

        await sender.SendAsync(datagram, target, ct);
    }

    private static async Task<ReceivedNotify> AwaitFirstNotifyAsync(
        ControlPoint controlPoint,
        Func<IPEndPoint> endPoint,
        CancellationToken ct,
        Action<IDisposable>? captureSubscription = null)
    {
        var received = new TaskCompletionSource<ReceivedNotify>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var registration = ct.Register(() => received.TrySetCanceled(ct));

        var subscription = controlPoint.NotifyObservable().Subscribe(
            notify => received.TrySetResult(notify),
            ex => received.TrySetException(ex));

        if (captureSubscription is null)
        {
            using (subscription)
            {
                await SendAliveAsync(endPoint(), ct);
                return await received.Task;
            }
        }

        captureSubscription(subscription);

        await SendAliveAsync(endPoint(), ct);
        return await received.Task;
    }

    [Fact]
    public void Construction_BindsNothing()
    {
        var (controlPoint, setupCount, _) = CreateLoopbackControlPoint();

        using (controlPoint)
        {
            Assert.Equal(0, setupCount());
            Assert.False(controlPoint.InterfacesMaterialized);

            // Merely obtaining the observables is not a subscription either.
            _ = controlPoint.NotifyObservable();
            _ = controlPoint.MSearchResponseObservable();

            Assert.Equal(0, setupCount());
        }
    }

    [Fact]
    public async Task FirstSubscription_StartsListening()
    {
        var (controlPoint, setupCount, endPoint) = CreateLoopbackControlPoint();
        using var _ = controlPoint;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var notify = await AwaitFirstNotifyAsync(controlPoint, endPoint, cts.Token);

        Assert.Equal(NTS.Alive, notify.NTS);
        Assert.Equal("device-1", notify.USN?.DeviceUUID);
        Assert.Equal(1, setupCount());
        Assert.True(controlPoint.InterfacesMaterialized);
    }

    [Fact]
    public async Task ConcurrentFirstSubscribers_SetUpSocketsOnce()
    {
        var (controlPoint, setupCount, _) = CreateLoopbackControlPoint();
        using var _guard = controlPoint;

        using var barrier = new Barrier(9);
        var subscriptions = new IDisposable[8];

        // Eight threads reach for the streams at the same instant — the race the
        // downstream "ensure started once" lock used to exist for.
        var racers = Enumerable.Range(0, 8).Select(i => Task.Run(() =>
        {
            barrier.SignalAndWait();

            subscriptions[i] = i % 2 == 0
                ? controlPoint.NotifyObservable().Subscribe(_ => { })
                : controlPoint.MSearchResponseObservable().Subscribe(_ => { });
        }, TestContext.Current.CancellationToken)).ToArray();

        barrier.SignalAndWait(TestContext.Current.CancellationToken);

        await Task.WhenAll(racers);

        Assert.Equal(1, setupCount());

        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }
    }

    [Fact]
    public async Task LastDisposal_ThenResubscription_KeepsWorking()
    {
        var (controlPoint, setupCount, endPoint) = CreateLoopbackControlPoint();
        using var _ = controlPoint;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var first = await AwaitFirstNotifyAsync(controlPoint, endPoint, cts.Token);
        Assert.Equal(NTS.Alive, first.NTS);

        // The subscription above is disposed by now: listening has stopped.
        // Re-subscribing must restart it on the same, still-bound socket
        // (requires SimpleHttpListener.Rx 7.3.0 restart tolerance).
        var second = await AwaitFirstNotifyAsync(controlPoint, endPoint, cts.Token);
        Assert.Equal(NTS.Alive, second.NTS);

        // Sockets were created once and reused across the restart.
        Assert.Equal(1, setupCount());
    }

    [Fact]
    public async Task SecondSubscriber_JoinsWithoutRestartingSockets()
    {
        var (controlPoint, setupCount, endPoint) = CreateLoopbackControlPoint();
        using var _ = controlPoint;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        IDisposable? held = null;

        var first = await AwaitFirstNotifyAsync(controlPoint, endPoint, cts.Token, s => held = s);
        Assert.Equal(NTS.Alive, first.NTS);

        using (held)
        {
            var second = await AwaitFirstNotifyAsync(controlPoint, endPoint, cts.Token);
            Assert.Equal(NTS.Alive, second.NTS);
        }

        Assert.Equal(1, setupCount());
    }

    [Fact]
    public async Task SendMSearch_MaterializesSockets_WithoutAnySubscription()
    {
        var (controlPoint, setupCount, _) = CreateLoopbackControlPoint();
        using var _guard = controlPoint;

        var request = new MulticastMSearch
        {
            MX = new MxSeconds(1),
            ST = new ST { StSearchType = STType.All },
            CPFN = "Test CP",
            SendCount = 1
        };

        // Sending is legal without a subscription; there is no start to get wrong.
        await controlPoint.SendMSearchAsync(request, IPAddress.Loopback, TestContext.Current.CancellationToken);

        Assert.Equal(1, setupCount());
    }

    [Fact]
    public async Task SendMSearch_AfterDispose_Throws()
    {
        var (controlPoint, _, _) = CreateLoopbackControlPoint();

        controlPoint.Dispose();

        var request = new MulticastMSearch { ST = new ST { StSearchType = STType.All }, CPFN = "Test CP" };

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => controlPoint.SendMSearchAsync(request, IPAddress.Loopback, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Dispose_WithoutUse_DoesNotBindOrThrow()
    {
        var (controlPoint, setupCount, _) = CreateLoopbackControlPoint();

        controlPoint.Dispose();

        Assert.Equal(0, setupCount());
    }
}
