using System.Buffers;
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
    /// Composes an M-SEARCH request datagram, multicast or unicast.
    /// </summary>
    /// <remarks>
    /// The two forms carry different header sets, and which one you get is decided
    /// by the type rather than by a flag: UDA 2.0 section 1.3.2 gives unicast search
    /// its own message format, without <c>MX</c>, <c>CPFN</c> or <c>TCPPORT</c>.
    /// </remarks>
    /// <param name="request">The request to compose; its <see cref="MSearchRequest.ST"/> must be fully specified.</param>
    /// <exception cref="SSDPException">The search target is not fully specified, or a multicast request has an empty CPFN.</exception>
    public static byte[] ComposeMSearchRequest(MSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var builder = Rent();

        builder.Append("M-SEARCH * HTTP/1.1\r\n");

        switch (request)
        {
            case MulticastMSearch multicast:
                ComposeMulticastMSearch(builder, multicast);
                break;
            case UnicastMSearch unicast:
                ComposeUnicastMSearch(builder, unicast);
                break;
            default:
                // Unreachable: the hierarchy is closed by a private protected
                // constructor, so there is no third case to forget.
                throw new SSDPException($"Unknown M-SEARCH request type: {request.GetType().Name}.");
        }

        return Terminate(builder);
    }

    private static void ComposeMulticastMSearch(StringBuilder builder, MulticastMSearch request)
    {
        // CPFN is Required for multicast search (UDA 2.0 section 1.3.2). The type
        // makes forgetting it impossible; this catches setting it to nothing, which
        // would put a blank required header on the wire.
        if (string.IsNullOrWhiteSpace(request.CPFN))
        {
            throw new SSDPException(
                "A multicast M-SEARCH requires a non-empty CPFN.UPNP.ORG (UDA 2.0 section 1.3.2).");
        }

        builder.Append($"{SsdpHeaders.Host}: {Constants.UdpSSDPMultiCastAddress}:{Constants.UdpSSDPMulticastPort}\r\n");
        builder.Append($"{SsdpHeaders.Man}: \"ssdp:discover\"\r\n");
        builder.Append($"{SsdpHeaders.Mx}: {request.MX.Seconds}\r\n");
        builder.Append($"{SsdpHeaders.St}: {request.ST.ToSearchTargetString()}\r\n");
        builder.Append($"{SsdpHeaders.UserAgent}: {request.UserAgent.ToHeaderString()}\r\n");
        builder.Append($"{SsdpHeaders.Cpfn}: {request.CPFN}\r\n");

        AppendOptional(builder, SsdpHeaders.Cpuuid, request.CPUUID);
        AppendOptional(builder, SsdpHeaders.TcpPort, request.TCPPORT?.ToString());

        AppendVendorHeaders(builder, request.Headers);
    }

    private static void ComposeUnicastMSearch(StringBuilder builder, UnicastMSearch request)
    {
        builder.Append($"{SsdpHeaders.Host}: {request.Host}\r\n");
        builder.Append($"{SsdpHeaders.Man}: \"ssdp:discover\"\r\n");
        builder.Append($"{SsdpHeaders.St}: {request.ST.ToSearchTargetString()}\r\n");
        builder.Append($"{SsdpHeaders.UserAgent}: {request.UserAgent.ToHeaderString()}\r\n");
    }

    /// <summary>
    /// Composes an M-SEARCH response datagram. The <c>DATE</c> header is taken from
    /// <see cref="MSearchResponse.Date"/>.
    /// </summary>
    /// <param name="response">The response to compose.</param>
    /// <exception cref="SSDPException">The search target or USN is not fully specified.</exception>
    public static byte[] ComposeMSearchResponse(MSearchResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var builder = Rent();

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
    /// <param name="notify">The notification to compose.</param>
    /// <exception cref="SSDPException">The USN is not fully specified.</exception>
    public static byte[] ComposeNotify(Notify notify)
    {
        ArgumentNullException.ThrowIfNull(notify);

        var builder = Rent();

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
            AppendOptional(builder, SsdpHeaders.SearchPort, notify.SEARCHPORT?.ToString());

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

        // ToString() would copy the whole message into a string only for GetBytes
        // to copy it straight back out again. Going through a pooled char buffer
        // leaves one allocation - the byte[] the caller gets, which is the only one
        // that outlives this method.
        var length = builder.Length;
        var chars = ArrayPool<char>.Shared.Rent(length);

        try
        {
            builder.CopyTo(0, chars, length);

            var text = chars.AsSpan(0, length);
            var datagram = new byte[Encoding.UTF8.GetByteCount(text)];

            Encoding.UTF8.GetBytes(text, datagram);

            return datagram;
        }
        finally
        {
            ArrayPool<char>.Shared.Return(chars);
            Release(builder);
        }
    }

    // SSDP datagrams have to fit in a single UDP packet, so a builder that has
    // grown past that was serving a message the protocol does not allow and is not
    // worth keeping alive per thread.
    private const int MaxRetainedBuilderCapacity = 2048;

    [ThreadStatic]
    private static StringBuilder? _cachedBuilder;

    // One builder per thread rather than one per message. Composition is
    // straight-line and never re-entrant, so the only way to observe this is
    // through the allocation count.
    private static StringBuilder Rent()
    {
        var builder = _cachedBuilder;

        if (builder is null)
        {
            return new StringBuilder(512);
        }

        _cachedBuilder = null;
        builder.Clear();

        return builder;
    }

    private static void Release(StringBuilder builder)
    {
        if (builder.Capacity <= MaxRetainedBuilderCapacity)
        {
            _cachedBuilder = builder;
        }
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
