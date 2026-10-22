using NodaTime;

namespace FixFlow.Domain;

/// <summary>
/// Soft-deleted (archived) rows stay in the database but are filtered out of every query. The aggregate sets
/// <see cref="DeletedAt"/> in its own archive method.
/// </summary>
public interface ISoftDeletable
{
    Instant? DeletedAt { get; }
}
