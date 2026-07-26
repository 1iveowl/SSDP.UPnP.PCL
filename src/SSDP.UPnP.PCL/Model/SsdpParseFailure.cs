using System.Collections.Frozen;
using System.Net;
using System.Text;
using SimpleHttpListener.Rx.Model;

namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// A message that arrived but could not be parsed as SSDP, and the reason why.
/// </summary>
/// <remarks>
/// Unparsable messages are dropped from the ordinary streams on purpose: a device
/// must silently discard malformed searches (UDA 2.0 section 1.3.3), and a control
/// point that threw on every odd datagram would be useless on a real network. That
/// silence is hard to debug from the outside, which is what this reports.
/// <para>
/// <see cref="RawMessage"/> carries the bytes as sent only when raw capture is
/// enabled on the control point or device; everything else here is available
/// either way.
/// </para>
/// </remarks>
public sealed record SsdpParseFailure
{
    /// <summary>Why parsing failed, from the parser that rejected it.</summary>
    public required string Error { get; init; }

    /// <summary>Whether the message was a request or a response.</summary>
    public MessageType MessageType { get; init; }

    /// <summary>The request method (<c>M-SEARCH</c>, <c>NOTIFY</c>), or <see langword="null"/> for responses.</summary>
    public string? Method { get; init; }

    /// <summary>The headers as the listener normalized them (uppercase, comma-joined).</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        FrozenDictionary<string, string>.Empty;

    /// <summary>
    /// The message exactly as it arrived, before any parsing or normalization, when
    /// raw capture is enabled. Empty otherwise, and always empty for messages that
    /// arrived over TCP.
    /// </summary>
    public ReadOnlyMemory<byte> RawMessage { get; init; }

    /// <summary>The local endpoint the message arrived on.</summary>
    public IPEndPoint? LocalIpEndPoint { get; init; }

    /// <summary>The endpoint the message came from.</summary>
    public IPEndPoint? RemoteIpEndPoint { get; init; }

    /// <summary>
    /// <see cref="RawMessage"/> decoded as UTF-8 for logging, with invalid sequences
    /// replaced rather than rejected. Empty when nothing was captured.
    /// </summary>
    /// <remarks>
    /// Lenient by design: real devices emit byte sequences no encoding decodes
    /// faithfully, and a diagnostic that throws on the input it is meant to explain
    /// would be worse than useless. Use <see cref="RawMessage"/> when the exact
    /// bytes matter.
    /// </remarks>
    public string RawMessageText() =>
        RawMessage.IsEmpty
            ? string.Empty
            : Encoding.UTF8.GetString(RawMessage.Span);

    internal static SsdpParseFailure From(HttpRequestResponse message, string error) => new()
    {
        Error = error,
        MessageType = message.MessageType,
        Method = message.Method,
        Headers = message.Headers,
        RawMessage = message.RawMessage,
        LocalIpEndPoint = message.LocalEndPoint,
        RemoteIpEndPoint = message.RemoteEndPoint
    };
}
