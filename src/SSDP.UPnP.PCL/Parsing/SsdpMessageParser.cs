using System.Collections.Frozen;
using System.Globalization;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Model;

namespace SSDP.UPnP.PCL.Parsing;

/// <summary>
/// Pure functions that turn parsed HTTP messages (from SimpleHttpListener.Rx)
/// into typed SSDP messages. No function in this class has side effects.
/// </summary>
/// <remarks>
/// Header dictionaries are expected to be case-insensitive, as produced by
/// SimpleHttpListener.Rx.
/// <para>
/// Leniency policy: requests are parsed strictly (a request with an invalid ST is
/// useless to a device and fails), while responses and notifications are parsed
/// leniently (real-world devices send malformed headers; fields that cannot be
/// parsed are left unset) — except that a response in which <em>neither</em> ST
/// nor USN can be parsed is rejected, since it identifies nothing.
/// </para>
/// </remarks>
public static class SsdpMessageParser
{
    // RFC 2774 namespace declared by UPnP 1.0 devices carrying the NLS header.
    private const string UpnpExtensionNamespace = "http://schemas.upnp.org/upnp/1/0/";

    private static readonly FrozenSet<string> MSearchRequestStandardHeaders = new[]
    {
        "HOST", "CACHE-CONTROL", "MAN", "MX", "ST", "USER-AGENT",
        "CPFN.UPNP.ORG", "CPUUID.UPNP.ORG", "TCPPORT.UPNP.ORG", "SECURELOCATION.UPNP.ORG"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> MSearchResponseStandardHeaders = new[]
    {
        "HOST", "CACHE-CONTROL", "LOCATION", "DATE", "EXT", "SERVER", "ST", "USN",
        "BOOTID.UPNP.ORG", "CONFIGID.UPNP.ORG", "SEARCHPORT.UPNP.ORG", "SECURELOCATION.UPNP.ORG"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> NotifyStandardHeaders = new[]
    {
        "HOST", "CACHE-CONTROL", "LOCATION", "NT", "NTS", "SERVER", "USN",
        "BOOTID.UPNP.ORG", "CONFIGID.UPNP.ORG",
        "SEARCHPORT.UPNP.ORG", "NEXTBOOTID.UPNP.ORG", "SECURELOCATION.UPNP.ORG"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Parses a received M-SEARCH request message, applying the UDA 2.0 validation
    /// rules a device must enforce: the <c>MAN</c> header must be
    /// <c>"ssdp:discover"</c>, a multicast search must carry an integer
    /// <c>MX &gt;= 1</c>, and a <c>TCPPORT.UPNP.ORG</c> value, when present, must be
    /// in 49152–65535. Invalid requests fail so callers can silently discard them
    /// (UDA 2.0 §1.3.3 — error responses are prohibited).
    /// </summary>
    /// <remarks>
    /// Multicast vs unicast is classified by the <c>HOST</c> header (the SSDP group
    /// address means multicast; a device address means a targeted unicast search),
    /// since both arrive on the same UDP socket.
    /// </remarks>
    /// <param name="request">A message with <see cref="MessageType.Request"/> and method <c>M-SEARCH</c>.</param>
    /// <returns>The parsed request, or a failure describing the problem.</returns>
    public static ParseResult<MSearchRequest> ParseMSearchRequest(HttpRequestResponse request)
    {
        var stResult = ST.Parse(GetHeaderValue(request.Headers, "ST"));

        if (!stResult.IsSuccess)
        {
            return ParseResult<MSearchRequest>.Failure(stResult.Error);
        }

        var host = GetHeaderValue(request.Headers, "HOST");

        if (string.IsNullOrEmpty(host))
        {
            return ParseResult<MSearchRequest>.Failure("M-SEARCH is missing the required HOST header.");
        }

        if (TrimQuotes(GetHeaderValue(request.Headers, "MAN")) != "ssdp:discover")
        {
            return ParseResult<MSearchRequest>.Failure(
                "M-SEARCH MAN header must be \"ssdp:discover\".");
        }

        var transportType = request.Transport == HttpTransport.Tcp || !IsMulticastHost(host)
            ? TransportType.Unicast
            : TransportType.Multicast;

        var mx = TimeSpan.Zero;

        if (transportType == TransportType.Multicast)
        {
            // UDA 2.0 §1.3.3: a multicast search without a valid MX (>= 1) must be
            // silently discarded. Unicast searches carry no MX and are answered
            // immediately.
            if (!int.TryParse(GetHeaderValue(request.Headers, "MX"), out var mxSeconds) || mxSeconds < 1)
            {
                return ParseResult<MSearchRequest>.Failure(
                    "Multicast M-SEARCH requires an integer MX header of 1 or greater.");
            }

            mx = TimeSpan.FromSeconds(mxSeconds);
        }

        int? tcpPort = null;
        var tcpPortValue = GetHeaderValue(request.Headers, "TCPPORT.UPNP.ORG");

        if (tcpPortValue is not null)
        {
            if (!int.TryParse(tcpPortValue, out var parsedTcpPort)
                || parsedTcpPort is < Constants.MinDynamicPort or > Constants.MaxDynamicPort)
            {
                return ParseResult<MSearchRequest>.Failure(
                    $"TCPPORT.UPNP.ORG must be an integer in the range {Constants.MinDynamicPort}-{Constants.MaxDynamicPort}.");
            }

            tcpPort = parsedTcpPort;
        }

        return ParseResult<MSearchRequest>.Success(new MSearchRequest
        {
            TransportType = transportType,
            HOST = host,
            MX = mx,
            ST = stResult.Value,
            UserAgent = ParseDeviceInfo<UserAgent>(GetHeaderValue(request.Headers, "USER-AGENT")),
            CPFN = GetHeaderValue(request.Headers, "CPFN.UPNP.ORG"),
            CPUUID = GetHeaderValue(request.Headers, "CPUUID.UPNP.ORG"),
            TCPPORT = tcpPort,
            Headers = AdditionalHeaders(request.Headers, MSearchRequestStandardHeaders),
            LocalIpEndPoint = request.LocalEndPoint,
            RemoteIpEndPoint = request.RemoteEndPoint,
            HasParsingError = request.HasParsingErrors
        });
    }

    private static bool IsMulticastHost(string host) =>
        host == Constants.UdpSSDPMultiCastAddress
        || host.StartsWith($"{Constants.UdpSSDPMultiCastAddress}:", StringComparison.Ordinal);

    private static string? TrimQuotes(string? value)
    {
        var trimmed = value?.Trim();

        return trimmed is { Length: >= 2 } && trimmed[0] == '"' && trimmed[^1] == '"'
            ? trimmed[1..^1]
            : trimmed;
    }

    /// <summary>
    /// Parses a received M-SEARCH response message. Lenient: unparsable ST or USN
    /// headers are left unset — but a response where neither can be parsed fails,
    /// since it identifies nothing.
    /// </summary>
    /// <param name="response">A message with <see cref="MessageType.Response"/>.</param>
    /// <returns>The parsed response, or a failure describing the problem.</returns>
    public static ParseResult<MSearchResponse> ParseMSearchResponse(HttpRequestResponse response)
    {
        var st = ST.Parse(GetHeaderValue(response.Headers, "ST"));
        var usn = USN.Parse(GetHeaderValue(response.Headers, "USN"));

        if (!st.IsSuccess && !usn.IsSuccess)
        {
            return ParseResult<MSearchResponse>.Failure(
                $"Neither ST nor USN could be parsed. ST: {st.Error} USN: {usn.Error}");
        }

        return ParseResult<MSearchResponse>.Success(new MSearchResponse
        {
            TransportType = ToTransportType(response.Transport),
            StatusCode = response.StatusCode,
            ResponseReason = response.ReasonPhrase ?? string.Empty,
            CacheControl = TimeSpan.FromSeconds(ParseMaxAge(GetHeaderValue(response.Headers, "CACHE-CONTROL"))),
            Date = ParseRfc1123Date(GetHeaderValue(response.Headers, "DATE")),
            NLS = ParseNls(response.Headers),
            Location = ParseUri(GetHeaderValue(response.Headers, "LOCATION")),
            Ext = response.Headers.ContainsKey("EXT"),
            Server = ParseDeviceInfo<Server>(GetHeaderValue(response.Headers, "SERVER")),
            ST = st.Value,
            USN = usn.Value,
            BOOTID = ParseNullableUInt(GetHeaderValue(response.Headers, "BOOTID.UPNP.ORG")),
            CONFIGID = ParseNullableInt(GetHeaderValue(response.Headers, "CONFIGID.UPNP.ORG")),
            SEARCHPORT = ParseNullableInt(GetHeaderValue(response.Headers, "SEARCHPORT.UPNP.ORG")),
            SECURELOCATION = GetHeaderValue(response.Headers, "SECURELOCATION.UPNP.ORG"),
            Headers = AdditionalHeaders(response.Headers, MSearchResponseStandardHeaders),
            LocalIpEndPoint = response.LocalEndPoint,
            RemoteIpEndPoint = response.RemoteEndPoint,
            HasParsingError = response.HasParsingErrors
        });
    }

    /// <summary>
    /// Parses a received NOTIFY request message. Lenient: unparsable fields (e.g. a
    /// malformed USN) are left unset rather than failing the whole message.
    /// </summary>
    /// <param name="request">A message with <see cref="MessageType.Request"/> and method <c>NOTIFY</c>.</param>
    /// <returns>The parsed notification, or a failure describing the problem.</returns>
    public static ParseResult<Notify> ParseNotify(HttpRequestResponse request)
    {
        var usn = USN.Parse(GetHeaderValue(request.Headers, "USN"));

        return ParseResult<Notify>.Success(new Notify
        {
            NotifyTransportType = ToTransportType(request.Transport),
            HOST = GetHeaderValue(request.Headers, "HOST"),
            CacheControl = TimeSpan.FromSeconds(ParseMaxAge(GetHeaderValue(request.Headers, "CACHE-CONTROL"))),
            Location = ParseUri(GetHeaderValue(request.Headers, "LOCATION")),
            NT = GetHeaderValue(request.Headers, "NT"),
            NTS = NTSExtensions.ToNTS(GetHeaderValue(request.Headers, "NTS")),
            Server = ParseDeviceInfo<Server>(GetHeaderValue(request.Headers, "SERVER")),
            USN = usn.Value,
            BOOTID = ParseNullableUInt(GetHeaderValue(request.Headers, "BOOTID.UPNP.ORG")),
            NLS = ParseNls(request.Headers),
            CONFIGID = ParseNullableInt(GetHeaderValue(request.Headers, "CONFIGID.UPNP.ORG")),
            SEARCHPORT = ParseNullableInt(GetHeaderValue(request.Headers, "SEARCHPORT.UPNP.ORG")),
            NEXTBOOTID = ParseNullableUInt(GetHeaderValue(request.Headers, "NEXTBOOTID.UPNP.ORG")),
            SECURELOCATION = GetHeaderValue(request.Headers, "SECURELOCATION.UPNP.ORG"),
            IsUuidUpnp2Compliant = Guid.TryParse(usn.Value?.DeviceUUID, out _),
            Headers = AdditionalHeaders(request.Headers, NotifyStandardHeaders),
            LocalIpEndPoint = request.LocalEndPoint,
            RemoteIpEndPoint = request.RemoteEndPoint,
            HasParsingError = request.HasParsingErrors
        });
    }

    /// <summary>
    /// Parses the SSDP <c>SERVER</c> / <c>USER-AGENT</c> header format:
    /// <c>OS/version UPnP/major.minor product/version</c>. Missing parts are left unset.
    /// </summary>
    /// <typeparam name="T">The <see cref="DeviceInfo"/>-derived record to produce.</typeparam>
    /// <param name="value">The raw header value; an empty result is returned when null or empty.</param>
    public static T ParseDeviceInfo<T>(string? value) where T : DeviceInfo, new()
    {
        if (string.IsNullOrEmpty(value))
        {
            return new T();
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var (os, osVersion) = parts.Length > 0 ? SplitPair(parts[0]) : (null, null);

        var (upnpMajor, upnpMinor) = FindUpnpVersion(parts);

        var (product, productVersion) = parts.Length > 2 ? SplitPair(parts[2]) : (null, null);

        return new T
        {
            FullString = value,
            OperatingSystem = os,
            OperatingSystemVersion = osVersion,
            UpnpMajorVersion = upnpMajor,
            UpnpMinorVersion = upnpMinor,
            ProductName = product,
            ProductVersion = productVersion
        };

        static (string?, string?) SplitPair(string part)
        {
            var pair = part.Split('/');
            return pair.Length == 2 ? (pair[0], pair[1]) : (part, null);
        }

        // UDA 2.0 section 1.1.2 puts the UPnP token second, and this library sends
        // it there, but real devices reorder the product tokens
        // ("UPnP/1.0, DLNADOC/1.50 Platinum/1.0.5.13"). Find the token by its name
        // instead of its position, and report absence rather than guessing when
        // there is none: an assumed version is worse than an unknown one.
        static (int?, int?) FindUpnpVersion(string[] parts)
        {
            foreach (var part in parts)
            {
                // Trailing commas are common in the reordered forms.
                var token = part.TrimEnd(',');
                var separator = token.IndexOf('/');

                if (separator < 0
                    || !token.AsSpan(0, separator).Equals("UPnP", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var version = token[(separator + 1)..].Split('.');

                if (version.Length == 2
                    && int.TryParse(version[0], out var major)
                    && int.TryParse(version[1], out var minor))
                {
                    return (major, minor);
                }

                return (null, null);
            }

            return (null, null);
        }
    }

    /// <summary>
    /// Extracts the <c>max-age</c> value in seconds from a <c>CACHE-CONTROL</c>
    /// header value; returns 0 when the directive is absent or unparsable.
    /// </summary>
    /// <remarks>
    /// The header is a comma-separated directive list (RFC 9111 section 5.2), so
    /// <c>max-age</c> may sit anywhere in it and alongside others. Directive names
    /// are case-insensitive. Values above <see cref="int.MaxValue"/> are clamped
    /// rather than rejected, per the delta-seconds guidance in RFC 9111 section
    /// 1.2.2, and negative values are treated as unparsable because a negative
    /// lifetime is meaningless downstream.
    /// <para>
    /// This is a directive walk, not a full HTTP tokenizer: a quoted directive
    /// value containing a comma would be split incorrectly. SSDP
    /// <c>CACHE-CONTROL</c> never carries one.
    /// </para>
    /// </remarks>
    public static int ParseMaxAge(string? cacheControl)
    {
        if (string.IsNullOrWhiteSpace(cacheControl))
        {
            return 0;
        }

        foreach (var directive in cacheControl.Split(','))
        {
            var separator = directive.IndexOf('=');

            if (separator < 0)
            {
                continue;
            }

            var name = directive.AsSpan(0, separator).Trim();

            if (!name.Equals("max-age", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = directive.AsSpan(separator + 1).Trim();

            // Lenient on receive: max-age="1800" is malformed but observed.
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                value = value[1..^1].Trim();
            }

            if (long.TryParse(value, out var seconds))
            {
                return seconds switch
                {
                    < 0 => 0,
                    > int.MaxValue => int.MaxValue,
                    _ => (int)seconds
                };
            }

            // Too large for a long, but still a plain number: clamp rather than
            // report the device said nothing.
            return !value.IsEmpty && value.IndexOfAnyExceptInRange('0', '9') < 0
                ? int.MaxValue
                : 0;
        }

        return 0;
    }

    /// <summary>
    /// Parses an RFC 1123 (RFC 9110) date header value; returns
    /// <see langword="null"/> when absent or malformed.
    /// </summary>
    public static DateTimeOffset? ParseRfc1123Date(string? value) =>
        DateTimeOffset.TryParseExact(value, "r", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    /// <summary>
    /// Resolves the UPnP 1.0 Network Location Signature from a message's headers.
    /// </summary>
    /// <remarks>
    /// The header is namespaced through RFC 2774's HTTP Extension Framework: the
    /// sender declares <c>OPT: "http://schemas.upnp.org/upnp/1/0/"; ns=01</c> and
    /// then sends <c>01-NLS</c>. The declared prefix is honoured only when the OPT
    /// names the UPnP namespace; otherwise, and when there is no usable OPT at
    /// all, any <c>NN-NLS</c> header is accepted, because some stacks emit the
    /// prefixed header without declaring it. Absence is never an error.
    /// </remarks>
    public static string? ParseNls(IReadOnlyDictionary<string, string> headers)
    {
        var opt = GetHeaderValue(headers, "OPT");

        if (opt is not null)
        {
            var parts = opt.Split(';');

            if (string.Equals(TrimQuotes(parts[0]), UpnpExtensionNamespace, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var part in parts.Skip(1))
                {
                    var trimmed = part.Trim();

                    if (trimmed.StartsWith("ns=", StringComparison.OrdinalIgnoreCase)
                        && headers.TryGetValue($"{trimmed[3..].Trim()}-NLS", out var declared))
                    {
                        return declared;
                    }
                }
            }
        }

        foreach (var header in headers)
        {
            if (IsNamespacedNlsHeader(header.Key))
            {
                return header.Value;
            }
        }

        return null;
    }

    // Matches the "NN-NLS" shape, e.g. "01-NLS".
    private static bool IsNamespacedNlsHeader(string name) =>
        name.Length == 6
        && char.IsAsciiDigit(name[0])
        && char.IsAsciiDigit(name[1])
        && name[2] == '-'
        && name.AsSpan(3).Equals("NLS", StringComparison.OrdinalIgnoreCase);

    internal static string? GetHeaderValue(IReadOnlyDictionary<string, string> headers, string key) =>
        headers.TryGetValue(key, out var value) ? value : null;

    private static TransportType ToTransportType(HttpTransport transport) => transport switch
    {
        HttpTransport.Tcp => TransportType.Unicast,
        HttpTransport.Udp => TransportType.Multicast,
        _ => TransportType.NoCast
    };

    private static IReadOnlyDictionary<string, string> AdditionalHeaders(
        IReadOnlyDictionary<string, string> headers,
        FrozenSet<string> standardHeaders)
    {
        List<KeyValuePair<string, string>>? extras = null;

        foreach (var header in headers)
        {
            if (!standardHeaders.Contains(header.Key))
            {
                (extras ??= []).Add(header);
            }
        }

        return extras is null
            ? FrozenDictionary<string, string>.Empty
            : new Dictionary<string, string>(extras, StringComparer.OrdinalIgnoreCase);
    }

    private static uint ParseUIntOr(string? value, uint fallback) =>
        uint.TryParse(value, out var result) ? result : fallback;

    private static int? ParseNullableInt(string? value) =>
        int.TryParse(value, out var result) ? result : null;

    private static uint? ParseNullableUInt(string? value) =>
        uint.TryParse(value, out var result) ? result : null;

    private static Uri? ParseUri(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null;
}
