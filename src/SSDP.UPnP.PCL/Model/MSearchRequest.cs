using System.Collections.Frozen;
using System.Net;

namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// An M-SEARCH request this control point is sending. Either
/// <see cref="MulticastMSearch"/> or <see cref="UnicastMSearch"/> - there is no
/// third kind, and the hierarchy is closed.
/// </summary>
/// <remarks>
/// <para>
/// The two transports carry different header sets by specification (UDA 2.0
/// section 1.3.2 gives them separate message formats), and separate types are how
/// that stops being something you can get wrong: a multicast search has no target
/// endpoint to omit, and a unicast search has no <c>MX</c>, <c>CPFN</c> or
/// <c>TCPPORT</c> to set by mistake.
/// </para>
/// <para>
/// A received search is <see cref="ReceivedMSearch"/>, not this. What a device
/// puts on the wire is not something this library gets to require anything of, so
/// the two directions are separate types with opposite defaults: everything here
/// is required or defaulted, everything there is nullable.
/// </para>
/// </remarks>
public abstract record MSearchRequest
{
    // Closes the hierarchy: only the two types below can derive, so consumers of a
    // MSearchRequest can switch on it exhaustively.
    private protected MSearchRequest()
    {
    }

    /// <summary>The search target.</summary>
    public required ST ST { get; init; }

    /// <summary>The control point identity sent in the <c>USER-AGENT</c> header.</summary>
    public UserAgent UserAgent { get; init; } = new();

    /// <summary>
    /// Additional vendor-specific headers to send. Note that each SSDP message must
    /// fit in a single UDP packet (UDA 2.0 section 1.2.2) - keep vendor headers
    /// small.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        FrozenDictionary<string, string>.Empty;
}

/// <summary>
/// A multicast M-SEARCH: the ordinary "who is out there?" discovery request, sent
/// to the SSDP group.
/// </summary>
public sealed record MulticastMSearch : MSearchRequest
{
    /// <summary>
    /// Friendly name of the control point (<c>CPFN.UPNP.ORG</c>). Required by
    /// UDA 2.0 section 1.3.2 for multicast search, which is why it is
    /// <see langword="required"/> here.
    /// </summary>
    /// <remarks>
    /// Must not be empty: <see langword="required"/> obliges you to set it, and the
    /// composer additionally rejects an empty value, since a present-but-blank
    /// required header is worse than a thoughtful one.
    /// </remarks>
    public required string CPFN { get; init; }

    /// <summary>
    /// Maximum response delay (<c>MX</c>). Defaults to the minimum of 1 second;
    /// see <see cref="MxSeconds"/> for why the upper bound is an advisory rather
    /// than a constraint.
    /// </summary>
    public MxSeconds MX { get; init; } = MxSeconds.Minimum;

    /// <summary>UUID of the control point (<c>CPUUID.UPNP.ORG</c>), if any.</summary>
    public string? CPUUID { get; init; }

    /// <summary>
    /// The TCP port for reliable search responses (<c>TCPPORT.UPNP.ORG</c>), if any.
    /// When set, devices reply over TCP to this port instead of UDP, without the
    /// <c>MX</c> spreading (UDA 2.0 section 1.3.3).
    /// </summary>
    public DynamicPort? TCPPORT { get; init; }

    /// <summary>
    /// How many times <see cref="IControlPoint.SendMSearchAsync"/> transmits the
    /// search. UDA 2.0 section 1.3.2 recommends sending each M-SEARCH more than
    /// once, since UDP is unreliable; defaults to 2. Values below 1 are treated
    /// as 1.
    /// </summary>
    public int SendCount { get; init; } = 2;
}

/// <summary>
/// A unicast M-SEARCH, aimed at one device whose address is already known.
/// </summary>
/// <remarks>
/// Carries only <c>HOST</c>, <c>MAN</c>, <c>ST</c> and <c>USER-AGENT</c>: UDA 2.0
/// section 1.3.2 gives the unicast form its own message format, without <c>MX</c>,
/// <c>CPFN</c> or <c>TCPPORT</c>. Those properties are absent here rather than
/// ignored.
/// </remarks>
public sealed record UnicastMSearch : MSearchRequest
{
    /// <summary>
    /// The device to search. Also supplies the <c>HOST</c> header, so the two can
    /// no longer disagree.
    /// </summary>
    public required IPEndPoint Target { get; init; }

    /// <summary>The <c>HOST</c> header value, <c>address:port</c> of <see cref="Target"/>.</summary>
    public string Host => $"{Target.Address}:{Target.Port}";
}
