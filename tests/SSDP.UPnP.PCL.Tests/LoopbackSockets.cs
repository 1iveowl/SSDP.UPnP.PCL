using System.Net;
using System.Net.Sockets;

namespace SSDP.UPnP.PCL.Tests;

/// <summary>
/// Binds loopback sockets on a port SSDP will accept.
/// </summary>
/// <remarks>
/// A device's unicast search port has to be 1900 or in the 49152-65535 range UDA
/// 2.0 allows for <c>SEARCHPORT.UPNP.ORG</c>, so tests cannot bind port 0 and take
/// whatever they are given. Picking a random in-range port and retrying past the
/// ones already taken is the workaround, and it lived in three copies before this.
/// </remarks>
internal static class LoopbackSockets
{
    private const int MaxAttempts = 20;

    /// <summary>
    /// Calls <paramref name="bind"/> with random in-range ports until one is free.
    /// </summary>
    internal static T WithRetry<T>(Func<int, T> bind)
    {
        for (var attempt = 0; ; attempt++)
        {
            var port = Random.Shared.Next(Constants.MinDynamicPort, Constants.MaxDynamicPort + 1);

            try
            {
                return bind(port);
            }
            catch (SocketException) when (attempt < MaxAttempts)
            {
            }
        }
    }

    /// <summary>A UDP client bound to loopback on an in-range port.</summary>
    internal static UdpClient Udp() =>
        WithRetry(port => new UdpClient(new IPEndPoint(IPAddress.Loopback, port)));

    /// <summary>
    /// A UDP client bound to the wildcard address on an in-range port: how
    /// multicast sockets are bound on Linux and macOS.
    /// </summary>
    internal static UdpClient WildcardUdp() =>
        WithRetry(port => new UdpClient(new IPEndPoint(IPAddress.Any, port)));

    /// <summary>A started TCP listener bound to loopback on an in-range port.</summary>
    internal static TcpListener Tcp() =>
        WithRetry(port =>
        {
            var listener = new TcpListener(new IPEndPoint(IPAddress.Loopback, port));

            listener.Start();

            return listener;
        });
}
