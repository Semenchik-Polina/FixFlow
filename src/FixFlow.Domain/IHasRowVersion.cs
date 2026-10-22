namespace FixFlow.Domain;

/// <summary>
/// Optimistic concurrency for aggregates edited concurrently (§10): <see cref="RowVersion"/> maps to PostgreSQL's
/// <c>xmin</c>, and saving over a stale version is a conflict (409).
/// </summary>
public interface IHasRowVersion
{
    uint RowVersion { get; }
}
