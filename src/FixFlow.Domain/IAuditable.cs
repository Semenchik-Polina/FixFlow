using NodaTime;

namespace FixFlow.Domain;

/// <summary>
/// Who created and last changed the row, and when. Persistence sets these on save from the clock and the current
/// user; a null user means the change wasn't made by a signed-in user (e.g. signup).
/// </summary>
public interface IAuditable
{
    Instant CreatedAt { get; }

    Guid? CreatedBy { get; }

    Instant? UpdatedAt { get; }

    Guid? UpdatedBy { get; }
}
