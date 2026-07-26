using System.Net;
using System.Net.Sockets;

namespace SSDP.UPnP.PCL.Internal;

/// <summary>
/// Creates the UDP sockets SSDP listens on. Both the control point and the
/// device bind the same way, and the platform differences below are subtle
/// enough that they are expressed exactly once.
/// </summary>
internal static class MulticastSocket
{
    /// <summary>
    /// Creates a UDP client bound for SSDP on <paramref name="port"/> and joined to
    /// the SSDP multicast group on the interface identified by
    /// <paramref name="interfaceAddress"/>.
    /// </summary>
    /// <remarks>
    /// The bind address differs by platform. On Windows a socket bound to the
    /// interface address still receives multicast for groups it joined; on Linux
    /// and macOS multicast is only delivered to sockets bound to the wildcard
    /// address, and the group join is what scopes the traffic to one interface.
    /// Getting this wrong makes a device or control point silently deaf, so it
    /// lives here rather than at each call site.
    /// </remarks>
    /// <param name="interfaceAddress">The local interface to join the group on.</param>
    /// <param name="port">The local port to bind, normally <see cref="Constants.UdpSSDPMulticastPort"/>.</param>
    /// <param name="multicastTtl">Time-to-live for outgoing multicast packets.</param>
    internal static UdpClient CreateJoined(IPAddress interfaceAddress, int port, int multicastTtl)
    {
        var udpClient = new UdpClient
        {
            MulticastLoopback = true
        };

        if (OperatingSystem.IsWindows())
        {
            udpClient.ExclusiveAddressUse = false;
        }

        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, multicastTtl);

        var bindAddress = OperatingSystem.IsWindows() ? interfaceAddress : IPAddress.Any;

        udpClient.Client.Bind(new IPEndPoint(bindAddress, port));
        udpClient.JoinMulticastGroup(IPAddress.Parse(Constants.UdpSSDPMultiCastAddress), interfaceAddress);

        return udpClient;
    }

    /// <summary>
    /// Creates a UDP client bound to <paramref name="ipEndPoint"/> for unicast
    /// traffic only, without joining any multicast group.
    /// </summary>
    internal static UdpClient CreateUnicast(IPEndPoint ipEndPoint)
    {
        var udpClient = new UdpClient();

        if (OperatingSystem.IsWindows())
        {
            udpClient.ExclusiveAddressUse = false;
        }

        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpClient.Client.Bind(ipEndPoint);

        return udpClient;
    }
}
