using System.Diagnostics.CodeAnalysis;

namespace SSDP.UPnP.PCL.Model;

/// <summary>
/// The outcome of parsing an SSDP message or header value: either a successfully
/// parsed <typeparamref name="T"/> or an error description. Immutable.
/// </summary>
/// <typeparam name="T">The type produced on success.</typeparam>
public sealed record ParseResult<T>
{
    private ParseResult(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    /// <summary>Whether parsing succeeded and <see cref="Value"/> is set.</summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    /// <summary>The parsed value; <see langword="null"/> when <see cref="IsSuccess"/> is <see langword="false"/>.</summary>
    public T? Value { get; }

    /// <summary>The parse error; <see langword="null"/> when <see cref="IsSuccess"/> is <see langword="true"/>.</summary>
    public string? Error { get; }

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    public static ParseResult<T> Success(T value) => new(true, value, null);

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    public static ParseResult<T> Failure(string error) => new(false, default, error);

    /// <summary>
    /// Applies <paramref name="onSuccess"/> or <paramref name="onFailure"/> depending on
    /// the outcome and returns the produced value.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(Error!);
}
