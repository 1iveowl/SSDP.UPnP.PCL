using System.Text;
using SSDP.UPnP.PCL.Model;

namespace SSDP.UPnP.PCL.Parsing;

/// <summary>
/// Pure functions that compose SSDP wire datagrams from message records. No
/// function in this class reads the clock or any other ambient state: everything
/// that appears in the datagram comes from the input.
/// </summary>
public static class DatagramComposer
{
    /// <summary>
    /// Composes an M-SEARCH request datagram.
    /// </summary>
    /// <param name="request">The request to compose; its <see cref="MSearchRequest.ST"/> must be fully specified.</param>
    /// <exception cref="SSDPException">The search target is not fully specified.</exception>
    public static byte[] ComposeMSearchRequest(MSearchRequest request)
    {
        var builder = new StringBuilder();

        builder.Append("M-SEARCH * HTTP/1.1\r\n");

        builder.Append(request.TransportType == TransportType.Multicast
            ? $"HOST: {Constants.UdpSSDPMultiCastAddress}:{Constants.UdpSSDPMulticastPort}\r\n"
            : $"HOST: {request.HOST}\r\n");

        builder.Append("MAN: \"ssdp:discover\"\r\n");

        if (request.TransportType == TransportType.Multicast)
        {
            builder.Append($"MX: {(int)request.MX.TotalSeconds}\r\n");
        }

        builder.Append($"ST: {request.ST.ToSearchTargetString()}\r\n");
        builder.Append($"USER-AGENT: {request.UserAgent.ToHeaderString()}\r\n");

        if (request.TransportType == TransportType.Multicast)
        {
            builder.Append($"CPFN.UPNP.ORG: {request.CPFN}\r\n");

            AppendOptional(builder, "CPUUID.UPNP.ORG", request.CPUUID);
            AppendOptional(builder, "TCPPORT.UPNP.ORG", request.TCPPORT);

            foreach (var header in request.Headers)
            {
                builder.Append($"{header.Key}: {header.Value}\r\n");
            }
        }

        builder.Append("\r\n");

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    /// <summary>
    /// Composes an M-SEARCH response datagram. The <c>DATE</c> header is taken from
    /// <see cref="MSearchResponse.Date"/>.
    /// </summary>
    /// <param name="response">The response to compose; <see cref="MSearchResponse.ST"/> and <see cref="MSearchResponse.USN"/> must be set.</param>
    /// <exception cref="SSDPException">The search target or USN is missing or not fully specified.</exception>
    public static byte[] ComposeMSearchResponse(MSearchResponse response)
    {
        if (response.ST is null || response.USN is null)
        {
            throw new SSDPException("An M-SEARCH response requires both ST and USN to be specified.");
        }

        var builder = new StringBuilder();

        builder.Append($"HTTP/1.1 {response.StatusCode} {response.ResponseReason}\r\n");
        builder.Append($"CACHE-CONTROL: max-age={(int)response.CacheControl.TotalSeconds}\r\n");
        builder.Append($"DATE: {response.Date:r}\r\n");
        builder.Append("EXT:\r\n");
        builder.Append($"LOCATION: {response.Location}\r\n");
        builder.Append($"SERVER: {response.Server.ToHeaderString()}\r\n");
        builder.Append($"ST: {(response.ST.StSearchType == STType.All ? response.ST.ToUriString() : response.ST.ToSearchTargetString())}\r\n");
        builder.Append($"USN: {response.USN.ToUsnString()}\r\n");
        builder.Append($"BOOTID.UPNP.ORG: {response.BOOTID}\r\n");

        AppendOptional(builder, "CONFIGID.UPNP.ORG", response.CONFIGID?.ToString());
        AppendOptional(builder, "SEARCHPORT.UPNP.ORG", response.SEARCHPORT?.ToString());
        AppendOptional(builder, "SECURELOCATION.UPNP.ORG", response.SECURELOCATION);

        foreach (var header in response.Headers)
        {
            builder.Append($"{header.Key}: {header.Value}\r\n");
        }

        builder.Append("\r\n");

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    /// <summary>
    /// Composes a NOTIFY datagram. Which headers are included follows the
    /// notification sub type: alive carries cache control, location and server;
    /// update carries location and NEXTBOOTID; byebye carries neither.
    /// </summary>
    /// <param name="notify">The notification to compose; <see cref="Notify.USN"/> must be set.</param>
    /// <exception cref="SSDPException">The USN is missing or not fully specified.</exception>
    public static byte[] ComposeNotify(Notify notify)
    {
        if (notify.USN is null)
        {
            throw new SSDPException("A NOTIFY message requires a USN to be specified.");
        }

        var builder = new StringBuilder();

        builder.Append("NOTIFY * HTTP/1.1\r\n");

        builder.Append(notify.NotifyTransportType == TransportType.Multicast
            ? $"HOST: {Constants.UdpSSDPMultiCastAddress}:{Constants.UdpSSDPMulticastPort}\r\n"
            : $"HOST: {notify.HOST}\r\n");

        if (notify.NTS == NTS.Alive)
        {
            builder.Append($"CACHE-CONTROL: max-age={(int)notify.CacheControl.TotalSeconds}\r\n");
        }

        if (notify.NTS is NTS.Alive or NTS.Update && notify.Location is not null)
        {
            builder.Append($"LOCATION: {notify.Location.AbsoluteUri}\r\n");
        }

        builder.Append($"NT: {notify.NT}\r\n");
        builder.Append($"NTS: {notify.NTS.ToUriString()}\r\n");

        if (notify.NTS == NTS.Alive && notify.Server is not null)
        {
            builder.Append($"SERVER: {notify.Server.ToHeaderString()}\r\n");
        }

        builder.Append($"USN: {notify.USN.ToUsnString()}\r\n");
        builder.Append($"BOOTID.UPNP.ORG: {notify.BOOTID}\r\n");

        AppendOptional(builder, "CONFIGID.UPNP.ORG", notify.CONFIGID?.ToString());

        if (notify.NTS == NTS.Update)
        {
            AppendOptional(builder, "NEXTBOOTID.UPNP.ORG", notify.NEXTBOOTID?.ToString());
        }

        if (notify.NTS is NTS.Alive or NTS.Update)
        {
            if (notify.SEARCHPORT is > 0 && notify.SEARCHPORT != Constants.UdpSSDPMulticastPort)
            {
                AppendOptional(builder, "SEARCHPORT.UPNP.ORG", notify.SEARCHPORT?.ToString());
            }

            AppendOptional(builder, "SECURELOCATION.UPNP.ORG", notify.SECURELOCATION);
        }

        foreach (var header in notify.Headers)
        {
            builder.Append($"{header.Key}: {header.Value}\r\n");
        }

        builder.Append("\r\n");

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void AppendOptional(StringBuilder builder, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            builder.Append($"{name}: {value}\r\n");
        }
    }
}
