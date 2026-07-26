using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace SSDP.UPnP.PCL;

/// <summary>
/// SSDP protocol constants and small network helpers.
/// </summary>
public static class Constants
{
    /// <summary>The SSDP IPv4 multicast group address.</summary>
    public const string UdpSSDPMultiCastAddress = "239.255.255.250";

    /// <summary>The SSDP multicast port.</summary>
    public const int UdpSSDPMulticastPort = 1900;

    /// <summary>
    /// The value of the <c>HOST</c> header on multicast SSDP messages,
    /// <c>239.255.255.250:1900</c>.
    /// </summary>
    public const string SsdpMulticastHost = UdpSSDPMultiCastAddress + ":" + "1900";

    /// <summary>
    /// The default local TCP port a control point listens on for unicast responses;
    /// inside the 49152–65535 range UDA 2.0 mandates for <c>TCPPORT.UPNP.ORG</c>.
    /// </summary>
    public const int TcpResponseListenerPort = 51900;

    /// <summary>Lowest port UDA 2.0 allows for SEARCHPORT.UPNP.ORG / TCPPORT.UPNP.ORG (RFC 4340 dynamic range).</summary>
    public const int MinDynamicPort = 49152;

    /// <summary>Highest port UDA 2.0 allows for SEARCHPORT.UPNP.ORG / TCPPORT.UPNP.ORG.</summary>
    public const int MaxDynamicPort = 65535;

    /// <summary>
    /// The default multicast time-to-live. UDA 2.0 recommends a TTL of 2 so
    /// discovery can cross one router while limiting congestion.
    /// </summary>
    public const int DefaultMulticastTtl = 2;

    /// <summary>
    /// Best-effort guess of the local IPv4 address to use for SSDP: the first
    /// non-loopback IPv4 address of an operational, gateway-connected interface.
    /// Returns <see langword="null"/> when no suitable address is found.
    /// </summary>
    public static IPAddress? GetBestGuessLocalIPAddress()
    {
        var candidates =
            from network in NetworkInterface.GetAllNetworkInterfaces()
            where network.OperationalStatus == OperationalStatus.Up
            where network.NetworkInterfaceType != NetworkInterfaceType.Tunnel
            let properties = network.GetIPProperties()
            where properties.GatewayAddresses.Any(gateway => gateway.Address.ToString() != "0.0.0.0")
            from address in properties.UnicastAddresses
            where address.Address.AddressFamily == AddressFamily.InterNetwork
            where !IPAddress.IsLoopback(address.Address)
            where !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                  || (address.IsDnsEligible && !address.IsTransient)
            select address.Address;

        return candidates.FirstOrDefault();
    }
}
