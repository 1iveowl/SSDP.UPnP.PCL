using System.Net;
using System.Net.Sockets;
using SSDP.UPnP.PCL.Internal;

namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// A network interface a <see cref="Device"/> advertises on: the root device
/// configuration plus the UDP clients used for multicast and unicast SSDP
/// traffic. The record itself is immutable; the sockets it references are live
/// resources owned by the creator.
/// </summary>
/// <remarks>
/// Use a separate <see cref="RootDeviceConfiguration"/> per interface: UDA 2.0
/// requires the <c>LOCATION</c> URL of each advertisement to be reachable on the
/// interface it is sent from, so multi-homed devices need per-interface
/// configurations (differing at least in <see cref="RootDeviceConfiguration.Location"/>).
/// </remarks>
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
    /// The port this interface answers unicast searches on, as advertised in
    /// <c>SEARCHPORT.UPNP.ORG</c>, or <see langword="null"/> when it listens on the
    /// default SSDP port (1900) and the header is therefore omitted.
    /// </summary>
    public int? SearchPort
    {
        get
        {
            var port = (UdpUnicastClient.Client.LocalEndPoint as IPEndPoint)?.Port;

            return port == Constants.UdpSSDPMulticastPort ? null : port;
        }
    }

    /// <summary>
    /// Whether a message that arrived on <paramref name="ipEndPoint"/> belongs to
    /// this interface.
    /// </summary>
    /// <remarks>
    /// Two cases have to be covered. A socket bound to a concrete address reports
    /// exactly that endpoint, so it can be compared directly. A socket bound to the
    /// wildcard address — which is how multicast is received on Linux and macOS —
    /// reports the address of the interface the datagram actually arrived on, which
    /// is matched against this interface's configured address instead. The latter is
    /// what makes multi-homed matching work on those platforms.
    /// </remarks>
    public bool IsMatchingInterface(IPEndPoint? ipEndPoint)
    {
        if (ipEndPoint is null)
        {
            return false;
        }

        return MatchesSocket(UdpMulticastClient, ipEndPoint)
               || MatchesSocket(UdpUnicastClient, ipEndPoint);

        bool MatchesSocket(UdpClient client, IPEndPoint arrivedOn)
        {
            if (client.Client.LocalEndPoint is not IPEndPoint local)
            {
                return false;
            }

            if (Equals(local, arrivedOn))
            {
                return true;
            }

            return local.Address.IsWildcard()
                   && local.Port == arrivedOn.Port
                   && RootDeviceConfiguration.IpEndPoint is { } configured
                   && Equals(configured.Address, arrivedOn.Address);
        }
    }
}
