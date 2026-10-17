namespace FixFlow.Domain;

/// <summary>
/// An aggregate root collects the domain events its methods raise. Saving dispatches them in two phases (ADR 0005):
/// in-transaction handlers first, post-commit handlers after the commit.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
