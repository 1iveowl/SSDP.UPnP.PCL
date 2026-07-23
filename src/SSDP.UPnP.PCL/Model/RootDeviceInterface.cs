using System.Net;
using System.Net.Sockets;

namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// A network interface a <see cref="Device"/> advertises on: the root device
/// configuration plus the UDP clients used for multicast and unicast SSDP
/// traffic. The record itself is immutable; the sockets it references are live
/// resources owned by the creator.
/// </summary>
public sealed record RootDeviceInterface
{
    /// <summary>The root device advertised on this interface.</summary>
    public required RootDeviceConfiguration RootDeviceConfiguration { get; init; }

    /// <summary>The UDP client joined to the SSDP multicast group.</summary>
    public required UdpClient UdpMulticastClient { get; init; }

    /// <summary>
    /// The UDP client for unicast traffic; may be the same instance as
    /// <see cref="UdpMulticastClient"/> when the device listens on port 1900 only.
    /// </summary>
    public required UdpClient UdpUnicastClient { get; init; }

    /// <summary>
    /// Whether <paramref name="ipEndPoint"/> is one of this interface's local UDP
    /// endpoints.
    /// </summary>
    public bool IsMatchingInterface(IPEndPoint? ipEndPoint) =>
        ipEndPoint is not null
        && (Equals(UdpMulticastClient.Client.LocalEndPoint as IPEndPoint, ipEndPoint)
            || Equals(UdpUnicastClient.Client.LocalEndPoint as IPEndPoint, ipEndPoint));
}
