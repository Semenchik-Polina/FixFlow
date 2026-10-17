namespace FixFlow.Application.Errors;

/// <summary>
/// The row changed since the caller read it (stale row version). The API answers 409. Persistence translates EF
/// Core's concurrency exception into this one (T0.9), so the API depends only on Application and Domain.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("The record was changed by someone else.")
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
