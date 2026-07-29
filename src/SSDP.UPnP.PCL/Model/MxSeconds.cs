using System.Globalization;

namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// The <c>MX</c> value of a multicast M-SEARCH: the maximum number of seconds a
/// device may wait before answering.
/// </summary>
/// <remarks>
/// <para>
/// UDA 2.0 section 1.3.2 sets two obligations, and they are different in kind:
/// the value <em>shall</em> be at least 1, and it <em>should</em> be at most 5.
/// This type enforces the first and deliberately does not enforce the second,
/// because the same clause allows the value to "be increased if a large number of
/// devices are expected to respond". A type that refused 6 would refuse something
/// the specification permits.
/// </para>
/// <para>
/// So the ceiling is left to the <c>SSDP001</c> analyzer, which reports it as the
/// advisory it is rather than an error. Exceeding it is not a failure but it is
/// silent: section 1.3.3 has devices assume 5 for anything larger, so a control
/// point asking for a thirty second spread quietly gets five.
/// </para>
/// <para>
/// The default value is one second, so <c>default(MxSeconds)</c> is valid rather
/// than a zero that the specification forbids.
/// </para>
/// </remarks>
public readonly struct MxSeconds : IEquatable<MxSeconds>, IComparable<MxSeconds>
{
    // Stored as an offset from the minimum so that the all-zero default is one
    // second - the smallest legal value - rather than an illegal zero.
    private readonly int _secondsAboveMinimum;

    /// <summary>The smallest value UDA 2.0 allows.</summary>
    public const int MinimumSeconds = 1;

    /// <summary>The largest value UDA 2.0 recommends; larger values are legal but advisory.</summary>
    public const int RecommendedMaximumSeconds = 5;

    /// <summary>The default: <see cref="MinimumSeconds"/> second.</summary>
    public static MxSeconds Minimum => default;

    /// <summary>The largest recommended value, 5 seconds.</summary>
    public static MxSeconds RecommendedMaximum => new(RecommendedMaximumSeconds);

    /// <summary>
    /// Creates an <c>MX</c> value of <paramref name="seconds"/> seconds.
    /// </summary>
    /// <param name="seconds">Seconds; must be at least <see cref="MinimumSeconds"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seconds"/> is below the minimum.</exception>
    public MxSeconds(int seconds)
    {
        if (seconds < MinimumSeconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(seconds),
                seconds,
                $"MX shall be at least {MinimumSeconds} second (UDA 2.0 section 1.3.2); a device silently discards a search with less.");
        }

        _secondsAboveMinimum = seconds - MinimumSeconds;
    }

    /// <summary>The value in whole seconds, as it appears in the <c>MX</c> header.</summary>
    public int Seconds => _secondsAboveMinimum + MinimumSeconds;

    /// <summary>The value as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan ToTimeSpan() => TimeSpan.FromSeconds(Seconds);

    /// <summary>
    /// Creates an <c>MX</c> value from a <see cref="TimeSpan"/>, truncated to whole
    /// seconds because the header carries an integer.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value truncates to less than <see cref="MinimumSeconds"/> second.</exception>
    public static MxSeconds FromTimeSpan(TimeSpan value) => new((int)value.TotalSeconds);

    /// <summary>Whether <paramref name="seconds"/> is a value this type can hold.</summary>
    public static bool IsValid(int seconds) => seconds >= MinimumSeconds;

    /// <inheritdoc />
    public bool Equals(MxSeconds other) => _secondsAboveMinimum == other._secondsAboveMinimum;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is MxSeconds other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _secondsAboveMinimum;

    /// <inheritdoc />
    public int CompareTo(MxSeconds other) => _secondsAboveMinimum.CompareTo(other._secondsAboveMinimum);

    /// <summary>The value as it appears in the header, e.g. <c>3</c>.</summary>
    public override string ToString() => Seconds.ToString(CultureInfo.InvariantCulture);

    /// <summary>Whether two values are equal.</summary>
    public static bool operator ==(MxSeconds left, MxSeconds right) => left.Equals(right);

    /// <summary>Whether two values differ.</summary>
    public static bool operator !=(MxSeconds left, MxSeconds right) => !left.Equals(right);

    /// <summary>Whether <paramref name="left"/> is shorter than <paramref name="right"/>.</summary>
    public static bool operator <(MxSeconds left, MxSeconds right) => left.CompareTo(right) < 0;

    /// <summary>Whether <paramref name="left"/> is longer than <paramref name="right"/>.</summary>
    public static bool operator >(MxSeconds left, MxSeconds right) => left.CompareTo(right) > 0;

    /// <summary>Whether <paramref name="left"/> is no longer than <paramref name="right"/>.</summary>
    public static bool operator <=(MxSeconds left, MxSeconds right) => left.CompareTo(right) <= 0;

    /// <summary>Whether <paramref name="left"/> is no shorter than <paramref name="right"/>.</summary>
    public static bool operator >=(MxSeconds left, MxSeconds right) => left.CompareTo(right) >= 0;
}
