using System.Globalization;
using SimpleHttpListener.Rx.Model;
using SSDP.UPnP.PCL.Model;

namespace SSDP.UPnP.PCL.Parsing;

/// <summary>
/// Pure functions that turn parsed HTTP messages (from SimpleHttpListener.Rx)
/// into typed SSDP messages. No function in this class has side effects.
/// </summary>
public static class SsdpMessageParser
{
    private static readonly string[] MSearchRequestStandardHeaders =
    [
        "HOST", "CACHE-CONTROL", "MAN", "MX", "ST", "USER-AGENT",
        "CPFN.UPNP.ORG", "CPUUID.UPNP.ORG", "TCPPORT.UPNP.ORG", "SECURELOCATION.UPNP.ORG"
    ];

    private static readonly string[] MSearchResponseStandardHeaders =
    [
        "HOST", "CACHE-CONTROL", "LOCATION", "DATE", "EXT", "SERVER", "ST", "USN",
        "BOOTID.UPNP.ORG", "CONFIGID.UPNP.ORG", "SEARCHPORT.UPNP.ORG", "SECURELOCATION.UPNP.ORG"
    ];

    private static readonly string[] NotifyStandardHeaders =
    [
        "HOST", "CACHE-CONTROL", "LOCATION", "NT", "NTS", "SERVER", "USN",
        "BOOTID.UPNP.ORG", "CONFIGID.UPNP.ORG",
        "SEARCHPORT.UPNP.ORG", "NEXTBOOTID.UPNP.ORG", "SECURELOCATION.UPNP.ORG"
    ];

    /// <summary>
    /// Parses a received M-SEARCH request message.
    /// </summary>
    /// <param name="request">A message with <see cref="MessageType.Request"/> and method <c>M-SEARCH</c>.</param>
    /// <returns>The parsed request, or a failure describing the problem.</returns>
    public static ParseResult<MSearchRequest> ParseMSearchRequest(HttpRequestResponse request)
    {
        var stResult = ST.Parse(GetHeaderValue(request.Headers, "ST"));

        if (!stResult.IsSuccess)
        {
            return ParseResult<MSearchRequest>.Failure(stResult.Error);
        }

        return ParseResult<MSearchRequest>.Success(new MSearchRequest
        {
            TransportType = ToTransportType(request.Transport),
            HOST = GetHeaderValue(request.Headers, "HOST"),
            MX = TimeSpan.FromSeconds(ParseIntOr(GetHeaderValue(request.Headers, "MX"), 0)),
            ST = stResult.Value,
            UserAgent = ParseDeviceInfo<UserAgent>(GetHeaderValue(request.Headers, "USER-AGENT")),
            CPFN = GetHeaderValue(request.Headers, "CPFN.UPNP.ORG"),
            CPUUID = GetHeaderValue(request.Headers, "CPUUID.UPNP.ORG"),
            TCPPORT = GetHeaderValue(request.Headers, "TCPPORT.UPNP.ORG"),
            Headers = AdditionalHeaders(request.Headers, MSearchRequestStandardHeaders),
            LocalIpEndPoint = request.LocalEndPoint,
            RemoteIpEndPoint = request.RemoteEndPoint,
            HasParsingError = request.HasParsingErrors
        });
    }

    /// <summary>
    /// Parses a received M-SEARCH response message.
    /// </summary>
    /// <param name="response">A message with <see cref="MessageType.Response"/>.</param>
    /// <returns>The parsed response, or a failure describing the problem.</returns>
    public static ParseResult<MSearchResponse> ParseMSearchResponse(HttpRequestResponse response)
    {
        var st = ST.Parse(GetHeaderValue(response.Headers, "ST"));
        var usn = USN.Parse(GetHeaderValue(response.Headers, "USN"));

        return ParseResult<MSearchResponse>.Success(new MSearchResponse
        {
            TransportType = ToTransportType(response.Transport),
            StatusCode = response.StatusCode,
            ResponseReason = response.ReasonPhrase ?? string.Empty,
            CacheControl = TimeSpan.FromSeconds(ParseMaxAge(GetHeaderValue(response.Headers, "CACHE-CONTROL"))),
            Date = ParseRfc1123Date(GetHeaderValue(response.Headers, "DATE")),
            Location = ParseUri(GetHeaderValue(response.Headers, "LOCATION")),
            Ext = response.Headers.ContainsKey("EXT"),
            Server = ParseDeviceInfo<Server>(GetHeaderValue(response.Headers, "SERVER")),
            ST = st.Value,
            USN = usn.Value,
            BOOTID = ParseUIntOr(GetHeaderValue(response.Headers, "BOOTID.UPNP.ORG"), 0),
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
    /// Parses a received NOTIFY request message.
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
            BOOTID = ParseUIntOr(GetHeaderValue(request.Headers, "BOOTID.UPNP.ORG"), 0),
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

        var (upnpMajor, upnpMinor, isUpnp2) = parts.Length > 1 ? ParseUpnpVersion(parts[1]) : ("1", "0", false);

        var (product, productVersion) = parts.Length > 2 ? SplitPair(parts[2]) : (null, null);

        return new T
        {
            FullString = value,
            OperatingSystem = os,
            OperatingSystemVersion = osVersion,
            UpnpMajorVersion = upnpMajor,
            UpnpMinorVersion = upnpMinor,
            IsUpnp2 = isUpnp2,
            ProductName = product,
            ProductVersion = productVersion
        };

        static (string?, string?) SplitPair(string part)
        {
            var pair = part.Split('/');
            return pair.Length == 2 ? (pair[0], pair[1]) : (part, null);
        }

        static (string, string, bool) ParseUpnpVersion(string part)
        {
            var pair = part.Split('/');

            if (pair.Length != 2)
            {
                return ("1", "0", false);
            }

            var version = pair[1].Split('.');

            return version.Length == 2
                ? (version[0], version[1], version[0] == "2")
                : ("1", "0", false);
        }
    }

    /// <summary>
    /// Extracts the <c>max-age</c> value in seconds from a <c>CACHE-CONTROL</c>
    /// header value; returns 0 when absent or malformed.
    /// </summary>
    public static int ParseMaxAge(string? cacheControl)
    {
        if (string.IsNullOrEmpty(cacheControl))
        {
            return 0;
        }

        var parts = cacheControl.Split('=');

        return parts.Length == 2 && int.TryParse(parts[1].Trim(), out var maxAge)
            ? maxAge
            : 0;
    }

    /// <summary>
    /// Parses an RFC 1123 (RFC 9110) date header value; returns
    /// <see cref="DateTimeOffset.MinValue"/> when absent or malformed.
    /// </summary>
    public static DateTimeOffset ParseRfc1123Date(string? value) =>
        DateTimeOffset.TryParseExact(value, "r", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : DateTimeOffset.MinValue;

    internal static string? GetHeaderValue(IReadOnlyDictionary<string, string> headers, string key) =>
        headers.TryGetValue(key.ToUpperInvariant(), out var value) ? value : null;

    private static TransportType ToTransportType(HttpTransport transport) => transport switch
    {
        HttpTransport.Tcp => TransportType.Unicast,
        HttpTransport.Udp => TransportType.Multicast,
        _ => TransportType.NoCast
    };

    private static IReadOnlyDictionary<string, string> AdditionalHeaders(
        IReadOnlyDictionary<string, string> headers,
        string[] standardHeaders)
    {
        var defaults = new HashSet<string>(standardHeaders, StringComparer.OrdinalIgnoreCase);

        return headers
            .Where(header => !defaults.Contains(header.Key))
            .ToDictionary(header => header.Key, header => header.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static int ParseIntOr(string? value, int fallback) =>
        int.TryParse(value, out var result) ? result : fallback;

    private static uint ParseUIntOr(string? value, uint fallback) =>
        uint.TryParse(value, out var result) ? result : fallback;

    private static int? ParseNullableInt(string? value) =>
        int.TryParse(value, out var result) ? result : null;

    private static uint? ParseNullableUInt(string? value) =>
        uint.TryParse(value, out var result) ? result : null;

    private static Uri? ParseUri(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null;
}
