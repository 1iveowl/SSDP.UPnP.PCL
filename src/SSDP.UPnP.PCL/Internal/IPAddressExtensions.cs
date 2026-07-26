using System.Net;

namespace SSDP.UPnP.PCL.Internal;

internal static class IPAddressExtensions
{
    /// <summary>
    /// Whether the address is a wildcard (<see cref="IPAddress.Any"/> or
    /// <see cref="IPAddress.IPv6Any"/>), meaning a socket bound to it is not tied
    /// to one network interface.
    /// </summary>
    internal static bool IsWildcard(this IPAddress? address) =>
        address is not null
        && (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any));
}
