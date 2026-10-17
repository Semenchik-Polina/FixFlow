using System.Diagnostics.CodeAnalysis;

namespace FixFlow.Application.Results;

/// <summary>
/// A handler's outcome: success, or an expected <see cref="ResultError"/> that the endpoint maps to Problem Details.
/// Validation failures, <c>DomainException</c> and <c>ConcurrencyConflictException</c> are thrown instead.
/// The factories return a <see cref="ResultError"/>, which converts to <see cref="Result"/> and
/// <see cref="Result{T}"/>, so <c>return Result.NotFound();</c> works in any handler.
/// </summary>
public class Result
{
    private protected Result(ResultError? error) => Error = error;

    public ResultError? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static ResultError NotFound() => new(ErrorType.NotFound);

    public static ResultError Unprocessable(string detail) => new(ErrorType.Unprocessable, detail);

    /// <summary>A 422 for one field, e.g. an unknown or foreign id in the body.</summary>
    public static ResultError FieldError(string field, string message) =>
        new(ErrorType.Unprocessable, FieldErrors: new Dictionary<string, string[]> { [field] = [message] });

    public static implicit operator Result(ResultError error) => new(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(null) => _value = value;

    private Result(ResultError error)
        : base(error)
    {
    }

    /// <summary>The value of a successful result; reading it from a failed one is a bug.</summary>
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("A failed result has no value.");

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(ResultError error) => new(error);
}
