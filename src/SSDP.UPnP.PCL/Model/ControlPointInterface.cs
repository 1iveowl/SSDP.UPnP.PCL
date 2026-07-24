using System.Net;
using System.Net.Sockets;

namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// A network interface a <see cref="ControlPoint"/> listens on: a UDP client
/// joined to the SSDP multicast group and, optionally, a TCP listener for
/// unicast responses. The record itself is immutable; the sockets it references
/// are live resources owned by the creator.
/// </summary>
public sealed record ControlPointInterface
{
    /// <summary>The local IP address of this interface.</summary>
    public required IPAddress IpAddress { get; init; }

    /// <summary>The UDP client used for multicast listening and M-SEARCH sending.</summary>
    public UdpClient? UdpClient { get; init; }

    /// <summary>The TCP listener for unicast responses, if any.</summary>
    public TcpListener? TcpListener { get; init; }
}
