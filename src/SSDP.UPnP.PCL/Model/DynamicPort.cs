using System.Globalization;

namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// A port in the 49152-65535 dynamic range, which is what UDA 2.0 requires of
/// <c>TCPPORT.UPNP.ORG</c> and <c>SEARCHPORT.UPNP.ORG</c>.
/// </summary>
/// <remarks>
/// <para>
/// The same range appears in three places - the TCP port a control point asks
/// devices to answer on, the port a device advertises for unicast search, and the
/// port a device binds for it - and used to be checked separately in each, strictly
/// on receive and not at all on send. One type, one check, and the asymmetry is
/// gone.
/// </para>
/// <para>
/// This does not make an out-of-range port <em>unrepresentable</em>: constructing
/// one still throws, it just throws where the mistake is rather than deep inside a
/// composer at send time. Catching it at build time is the <c>SSDP003</c>
/// analyzer's job, and only for a value the compiler can see.
/// </para>
/// <para>
/// There is no meaningful default, so this is a nullable-by-convention type: use
/// <c>DynamicPort?</c> for "no port advertised" rather than relying on
/// <c>default</c>, which is not a legal port.
/// </para>
/// </remarks>
public readonly struct DynamicPort : IEquatable<DynamicPort>
{
    // Offset from the minimum so that default(DynamicPort) is the bottom of the
    // range rather than 0, which is not a port at all.
    private readonly ushort _portAboveMinimum;

    /// <summary>The lowest port UDA 2.0 allows (RFC 4340 dynamic range).</summary>
    public const int MinimumPort = Constants.MinDynamicPort;

    /// <summary>The highest port UDA 2.0 allows.</summary>
    public const int MaximumPort = Constants.MaxDynamicPort;

    /// <summary>
    /// Creates a port in the dynamic range.
    /// </summary>
    /// <param name="port">The port; must be in <see cref="MinimumPort"/>-<see cref="MaximumPort"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> is outside the range.</exception>
    public DynamicPort(int port)
    {
        if (port is < MinimumPort or > MaximumPort)
        {
            throw new ArgumentOutOfRangeException(
                nameof(port),
                port,
                $"The port shall be in the range {MinimumPort}-{MaximumPort} (UDA 2.0 section 1.3.2, RFC 4340).");
        }

        _portAboveMinimum = (ushort)(port - MinimumPort);
    }

    /// <summary>The port number.</summary>
    public int Port => _portAboveMinimum + MinimumPort;

    /// <summary>Whether <paramref name="port"/> is a value this type can hold.</summary>
    public static bool IsValid(int port) => port is >= MinimumPort and <= MaximumPort;

    /// <summary>
    /// Creates a port when <paramref name="port"/> is in range, or
    /// <see langword="null"/> when it is not - for parsing, where an out-of-range
    /// value is the sender's mistake rather than the caller's.
    /// </summary>
    public static DynamicPort? TryCreate(int? port) =>
        port is { } value && IsValid(value) ? new DynamicPort(value) : null;

    /// <inheritdoc />
    public bool Equals(DynamicPort other) => _portAboveMinimum == other._portAboveMinimum;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DynamicPort other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _portAboveMinimum;

    /// <summary>The port as it appears in the header, e.g. <c>51900</c>.</summary>
    public override string ToString() => Port.ToString(CultureInfo.InvariantCulture);

    /// <summary>Whether two ports are equal.</summary>
    public static bool operator ==(DynamicPort left, DynamicPort right) => left.Equals(right);

    /// <summary>Whether two ports differ.</summary>
    public static bool operator !=(DynamicPort left, DynamicPort right) => !left.Equals(right);
}
