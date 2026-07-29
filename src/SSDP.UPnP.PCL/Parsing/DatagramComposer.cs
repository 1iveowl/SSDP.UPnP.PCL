using System.Text;
using SSDP.UPnP.PCL.Internal;
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
    /// <exception cref="SSDPException">
    /// The search target is not fully specified, or a multicast request has an MX
    /// below 1 second (UDA 2.0 requires <c>MX &gt;= 1</c>; compliant devices
    /// silently discard such requests).
    /// </exception>
    public static byte[] ComposeMSearchRequest(MSearchRequest request)
    {
        var builder = new StringBuilder();

        builder.Append("M-SEARCH * HTTP/1.1\r\n");

        builder.Append(request.TransportType == TransportType.Multicast
            ? $"{SsdpHeaders.Host}: {Constants.UdpSSDPMultiCastAddress}:{Constants.UdpSSDPMulticastPort}\r\n"
            : $"{SsdpHeaders.Host}: {request.HOST}\r\n");

        builder.Append($"{SsdpHeaders.Man}: \"ssdp:discover\"\r\n");

        if (request.TransportType == TransportType.Multicast)
        {
            if (request.MX < TimeSpan.FromSeconds(1))
            {
                throw new SSDPException("A multicast M-SEARCH requires an MX of at least 1 second (UDA 2.0 section 1.3.2).");
            }

            builder.Append($"{SsdpHeaders.Mx}: {(int)request.MX.TotalSeconds}\r\n");
        }

        builder.Append($"{SsdpHeaders.St}: {request.ST.ToSearchTargetString()}\r\n");
        builder.Append($"{SsdpHeaders.UserAgent}: {request.UserAgent.ToHeaderString()}\r\n");

        if (request.TransportType == TransportType.Multicast)
        {
            builder.Append($"{SsdpHeaders.Cpfn}: {request.CPFN}\r\n");

            AppendOptional(builder, SsdpHeaders.Cpuuid, request.CPUUID);
            AppendOptional(builder, SsdpHeaders.TcpPort, request.TCPPORT?.ToString());

            AppendVendorHeaders(builder, request.Headers);
        }

        return Terminate(builder);
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
        builder.Append($"{SsdpHeaders.CacheControl}: max-age={(int)MaxAgeOf(response.MaxAge).TotalSeconds}\r\n");
        // DATE is Recommended rather than Required (UDA 2.0 section 1.3.3), so a
        // response without one omits the header instead of emitting a placeholder.
        if (response.Date is { } date)
        {
            builder.Append($"{SsdpHeaders.Date}: {date:r}\r\n");
        }

        builder.Append($"{SsdpHeaders.Ext}:\r\n");
        builder.Append($"{SsdpHeaders.Location}: {response.Location}\r\n");
        builder.Append($"{SsdpHeaders.Server}: {response.Server.ToHeaderString()}\r\n");
        builder.Append($"{SsdpHeaders.St}: {(response.ST.StSearchType == STType.All ? response.ST.ToUriString() : response.ST.ToSearchTargetString())}\r\n");
        builder.Append($"{SsdpHeaders.Usn}: {response.USN.ToUsnString()}\r\n");
        builder.Append($"{SsdpHeaders.BootId}: {response.BOOTID ?? 0}\r\n");

        AppendOptional(builder, SsdpHeaders.ConfigId, response.CONFIGID?.ToString());
        AppendOptional(builder, SsdpHeaders.SearchPort, response.SEARCHPORT?.ToString());
        AppendOptional(builder, SsdpHeaders.SecureLocation, response.SECURELOCATION);

        AppendVendorHeaders(builder, response.Headers);

        return Terminate(builder);
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
            ? $"{SsdpHeaders.Host}: {Constants.UdpSSDPMultiCastAddress}:{Constants.UdpSSDPMulticastPort}\r\n"
            : $"{SsdpHeaders.Host}: {notify.HOST}\r\n");

        if (notify.NTS == NTS.Alive)
        {
            builder.Append($"{SsdpHeaders.CacheControl}: max-age={(int)MaxAgeOf(notify.MaxAge).TotalSeconds}\r\n");
        }

        if (notify.NTS is NTS.Alive or NTS.Update && notify.Location is not null)
        {
            builder.Append($"{SsdpHeaders.Location}: {notify.Location.AbsoluteUri}\r\n");
        }

        builder.Append($"{SsdpHeaders.Nt}: {notify.NT}\r\n");
        builder.Append($"{SsdpHeaders.Nts}: {notify.NTS.ToUriString()}\r\n");

        if (notify.NTS == NTS.Alive && notify.Server is not null)
        {
            builder.Append($"{SsdpHeaders.Server}: {notify.Server.ToHeaderString()}\r\n");
        }

        builder.Append($"{SsdpHeaders.Usn}: {notify.USN.ToUsnString()}\r\n");
        builder.Append($"{SsdpHeaders.BootId}: {notify.BOOTID ?? 0}\r\n");

        AppendOptional(builder, SsdpHeaders.ConfigId, notify.CONFIGID?.ToString());

        if (notify.NTS == NTS.Update)
        {
            AppendOptional(builder, SsdpHeaders.NextBootId, notify.NEXTBOOTID?.ToString());
        }

        if (notify.NTS is NTS.Alive or NTS.Update)
        {
            if (notify.SEARCHPORT is > 0 && notify.SEARCHPORT != Constants.UdpSSDPMulticastPort)
            {
                AppendOptional(builder, SsdpHeaders.SearchPort, notify.SEARCHPORT?.ToString());
            }

            AppendOptional(builder, SsdpHeaders.SecureLocation, notify.SECURELOCATION);
        }

        AppendVendorHeaders(builder, notify.Headers);

        return Terminate(builder);
    }

    // Vendor-specific headers are appended verbatim after the standard ones.
    private static void AppendVendorHeaders(StringBuilder builder, IReadOnlyDictionary<string, string> headers)
    {
        foreach (var header in headers)
        {
            builder.Append($"{header.Key}: {header.Value}\r\n");
        }
    }

    // Every SSDP message ends with the blank line that terminates the header block.
    private static byte[] Terminate(StringBuilder builder)
    {
        builder.Append("\r\n");

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    // CACHE-CONTROL is Required on the messages that carry it (UDA 2.0 section
    // 1.2.2), so composing never omits it. A message that announces no lifetime
    // still has to carry the header, and zero is the honest value for "expire me
    // now" - which is what an unset MaxAge asks for.
    private static TimeSpan MaxAgeOf(TimeSpan? maxAge) => maxAge ?? TimeSpan.Zero;

    private static void AppendOptional(StringBuilder builder, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            builder.Append($"{name}: {value}\r\n");
        }
    }
}
