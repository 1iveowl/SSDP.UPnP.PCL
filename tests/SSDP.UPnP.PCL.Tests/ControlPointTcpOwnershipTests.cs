using System.Net;
using System.Net.Sockets;
using System.Text;
using SSDP.UPnP.PCL.Model;
using Xunit;

namespace SSDP.UPnP.PCL.Tests;

/// <summary>
/// The control point's TCP response listener hands connection ownership back when
/// it stops reading, and the control point has to release it: it answers nothing it
/// receives, so no response closes the connection on its behalf.
/// </summary>
/// <remarks>
/// <para>
/// Externally triggerable - the peer decides, by sending HTTP/1.0, a
/// <c>Connection: close</c>, or an upgrade request - so a leak here is unbounded
/// rather than merely untidy.
/// </para>
/// <para>
/// Note which message shapes can actually leak. A message only transfers ownership
/// once it is emitted, and an <em>unframed</em> response (no <c>Content-Length</c>,
/// no <c>Transfer-Encoding</c>) carrying <c>Connection: close</c> is close-delimited
/// per RFC 9112 section 6.3, so it is not emitted until the sender closes the
/// connection - at which point there is nothing left to leak. Requests are emitted
/// immediately, because a request with no framing headers has no body (RFC 9112
/// section 6), and a <em>framed</em> response is emitted immediately too. Those are
/// the shapes tested here.
/// </para>
/// </remarks>
public class ControlPointTcpOwnershipTests
{
    // A NOTIFY the control point would otherwise consume, so the test exercises the
    // real pipeline rather than a message it ignores.
    private const string NotifyConnectionClose =
        "NOTIFY * HTTP/1.1\r\n" +
        "HOST: 239.255.255.250:1900\r\n" +
        "CACHE-CONTROL: max-age=1800\r\n" +
        "LOCATION: http://127.0.0.1/description.xml\r\n" +
        "NT: upnp:rootdevice\r\n" +
        "NTS: ssdp:alive\r\n" +
        "USN: uuid:device-1::upnp:rootdevice\r\n" +
        "CONNECTION: close\r\n" +
        "\r\n";

    private const string NotifyHttp10 =
        "NOTIFY * HTTP/1.0\r\n" +
        "HOST: 239.255.255.250:1900\r\n" +
        "NT: upnp:rootdevice\r\n" +
        "NTS: ssdp:alive\r\n" +
        "USN: uuid:device-1::upnp:rootdevice\r\n" +
        "\r\n";

    // Framed, so it is emitted at once rather than waiting for the sender to close.
    private const string FramedResponseConnectionClose =
        "HTTP/1.1 200 OK\r\n" +
        "CACHE-CONTROL: max-age=1800\r\n" +
        "EXT:\r\n" +
        "LOCATION: http://127.0.0.1/description.xml\r\n" +
        "SERVER: Linux/6.1 UPnP/2.0 Test/1.0\r\n" +
        "ST: upnp:rootdevice\r\n" +
        "USN: uuid:device-1::upnp:rootdevice\r\n" +
        "CONTENT-LENGTH: 0\r\n" +
        "CONNECTION: close\r\n" +
        "\r\n";

    private const string UpgradeRequest =
        "GET /chat HTTP/1.1\r\n" +
        "HOST: 127.0.0.1\r\n" +
        "UPGRADE: websocket\r\n" +
        "CONNECTION: Upgrade\r\n" +
        "SEC-WEBSOCKET-KEY: dGhlIHNhbXBsZSBub25jZQ==\r\n" +
        "SEC-WEBSOCKET-VERSION: 13\r\n" +
        "\r\n";

    // The server end closing is observable from the client as a zero-length read.
    // Unfixed, the connection stays open and this waits until the token trips.
    private static async Task AssertServerClosesAsync(string message, CancellationToken ct)
    {
        var listener = LoopbackSockets.Tcp();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var controlPoint = new ControlPoint(
            new ControlPointInterface { IpAddress = IPAddress.Loopback, TcpListener = listener });

        // Any subscription starts the listener. Subscribing to both parsed streams
        // also proves the message still reaches them after the connection is
        // released - releasing it must not cost the control point the message.
        using var notifySubscription = controlPoint.NotifyObservable().Subscribe(_ => { });
        using var responseSubscription = controlPoint.MSearchResponseObservable().Subscribe(_ => { });

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, ct);

        var stream = client.GetStream();

        await stream.WriteAsync(Encoding.ASCII.GetBytes(message), ct);
        await stream.FlushAsync(ct);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        var buffer = new byte[1];

        var read = await stream.ReadAsync(buffer, timeout.Token);

        Assert.Equal(0, read);
    }

    [Fact]
    public async Task RequestWithConnectionCloseHasItsConnectionReleased() =>
        await AssertServerClosesAsync(NotifyConnectionClose, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Http10RequestHasItsConnectionReleased() =>
        await AssertServerClosesAsync(NotifyHttp10, TestContext.Current.CancellationToken);

    [Fact]
    public async Task FramedResponseWithConnectionCloseHasItsConnectionReleased() =>
        await AssertServerClosesAsync(FramedResponseConnectionClose, TestContext.Current.CancellationToken);

    // ShouldKeepAlive is true for an upgrade request, so this is the branch a
    // keep-alive-only check would miss.
    [Fact]
    public async Task UpgradeRequestHasItsConnectionReleased() =>
        await AssertServerClosesAsync(UpgradeRequest, TestContext.Current.CancellationToken);

    // The complement: a keep-alive message must keep its connection, or a device
    // sending several responses down one connection would be truncated after the
    // first - which is exactly what Device.SendResponsesOverTcpAsync does.
    [Fact]
    public async Task KeepAliveMessagesKeepTheirConnectionOpen()
    {
        var ct = TestContext.Current.CancellationToken;

        var listener = LoopbackSockets.Tcp();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var controlPoint = new ControlPoint(
            new ControlPointInterface { IpAddress = IPAddress.Loopback, TcpListener = listener });

        var received = 0;

        using var subscription = controlPoint.NotifyObservable()
            .Subscribe(_ => Interlocked.Increment(ref received));

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, ct);

        var stream = client.GetStream();

        var keepAlive = NotifyConnectionClose.Replace("CONNECTION: close\r\n", string.Empty, StringComparison.Ordinal);
        var datagram = Encoding.ASCII.GetBytes(keepAlive);

        // Strictly one at a time, each awaited before the next is written. Writing
        // both up front would leave them in the socket buffer to be read together,
        // and the assertion would then pass even if the connection were closed
        // after the first - which is exactly the mutation this test exists to catch.
        for (var message = 1; message <= 2; message++)
        {
            await stream.WriteAsync(datagram, ct);
            await stream.FlushAsync(ct);

            for (var attempt = 0; attempt < 200 && Volatile.Read(ref received) < message; attempt++)
            {
                await Task.Delay(50, ct);
            }

            Assert.Equal(message, Volatile.Read(ref received));
        }
    }
}
