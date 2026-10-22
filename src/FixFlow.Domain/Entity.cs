namespace FixFlow.Domain;

/// <summary>
/// An entity with a UUIDv7 id, assigned when the object is created, so it's known before the insert (D10).
/// Persistence never generates it.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
}
