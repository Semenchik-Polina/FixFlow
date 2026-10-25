namespace FixFlow.Application.Errors;

/// <summary>
/// A save broke a unique index (plan P27). Persistence translates PostgreSQL's unique violation into this one, so
/// Application can tell which index without seeing Npgsql. A handler maps its own indexes
/// (<see cref="Persistence.UniqueIndexes"/>) to <c>Result.FieldError</c>; any other is a bug and stays a 500. The message
/// holds the index's name only, so logs keep it.
/// </summary>
public sealed class UniqueConstraintViolationException(string constraintName, Exception innerException)
    : Exception($"A save violated the unique constraint {constraintName}.", innerException), ILogSafeMessage
{
    /// <summary>The index's name, as the configuration gives it.</summary>
    public string ConstraintName { get; } = constraintName;
}
