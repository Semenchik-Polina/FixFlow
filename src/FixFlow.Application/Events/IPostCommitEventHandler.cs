using System.Diagnostics.CodeAnalysis;
using FixFlow.Domain;

namespace FixFlow.Application.Events;

/// <summary>
/// Handles a domain event after its transaction committed (ADR 0005): side effects such as email or cache
/// invalidation. Best effort: an exception is logged (event and handler type only) and never fails the action.
/// Never write business data here. Runs without the request's cancellation token, so a client that disconnects
/// after the commit doesn't cancel the side effect.
/// </summary>
[SuppressMessage("Naming", "CA1711", Justification = "Handles domain events, not .NET events (plan P12)")]
public interface IPostCommitEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}
