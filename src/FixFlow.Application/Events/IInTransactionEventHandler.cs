using System.Diagnostics.CodeAnalysis;
using FixFlow.Domain;

namespace FixFlow.Application.Events;

/// <summary>
/// Handles a domain event while it is being saved, before anything is written (ADR 0005). Use it to keep other
/// aggregates consistent (work order Closed → request Closed): change tracked aggregates only, through their domain
/// methods. Its changes commit or roll back with the change that raised the event, and an exception fails the save.
/// Never send email or call HTTP, Blob or other external services here: use <see cref="IPostCommitEventHandler{TEvent}"/>.
/// </summary>
[SuppressMessage("Naming", "CA1711", Justification = "Handles domain events, not .NET events (plan P12)")]
public interface IInTransactionEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}
