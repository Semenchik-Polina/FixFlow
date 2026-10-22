using System.Collections.Concurrent;
using FixFlow.Application.Events;
using FixFlow.Domain;
using FixFlow.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FixFlow.Infrastructure.Persistence.Events;

/// <summary>
/// Calls the handlers of a domain event (ADR 0005, plan P12). Handlers come from the request's scope, so
/// in-transaction handlers change aggregates tracked by the same DbContext that is saving.
/// </summary>
public sealed partial class DomainEventDispatcher(
    IServiceProvider services,
    IOptions<ExceptionLoggingOptions> exceptionLogging,
    ILogger<DomainEventDispatcher> logger)
{
    private static readonly ConcurrentDictionary<Type, EventInvoker> _invokers = new();

    /// <summary>One after another; an exception propagates and fails the save.</summary>
    public Task DispatchInTransactionAsync(IDomainEvent domainEvent, CancellationToken cancellationToken) =>
        InvokerFor(domainEvent).InTransactionAsync(services, domainEvent, cancellationToken);

    /// <summary>Every handler runs, whatever the others do: failures are logged and never thrown.</summary>
    public Task DispatchPostCommitAsync(IDomainEvent domainEvent) =>
        InvokerFor(domainEvent).PostCommitAsync(services, domainEvent, this);

    private static EventInvoker InvokerFor(IDomainEvent domainEvent) =>
        _invokers.GetOrAdd(
            domainEvent.GetType(),
            type => (EventInvoker)Activator.CreateInstance(typeof(EventInvoker<>).MakeGenericType(type))!);

    /// <summary>
    /// The exception's message can hold personal data (an SMTP error quoting the address), and log masking doesn't
    /// cover exceptions (S26). Outside Development, logs get its types and stack traces only (ADR 0007).
    /// </summary>
    private Exception ForLog(Exception exception) =>
        exceptionLogging.Value.KeepMessages ? exception : RedactedException.For(exception);

    // Types and IDs only: the exception carries the details, and the event's data stays out of the logs (S26)
    [LoggerMessage(Level = LogLevel.Error, Message = "Post-commit handler {Handler} failed for {DomainEvent}")]
    private partial void LogPostCommitFailure(Exception exception, string handler, string domainEvent);

    /// <summary>Resolves and calls the handlers of one event type, without reflection per call.</summary>
    private abstract class EventInvoker
    {
        public abstract Task InTransactionAsync(
            IServiceProvider services, IDomainEvent domainEvent, CancellationToken cancellationToken);

        public abstract Task PostCommitAsync(
            IServiceProvider services, IDomainEvent domainEvent, DomainEventDispatcher dispatcher);
    }

    private sealed class EventInvoker<TEvent> : EventInvoker
        where TEvent : IDomainEvent
    {
        public override async Task InTransactionAsync(
            IServiceProvider services, IDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            foreach (var handler in services.GetServices<IInTransactionEventHandler<TEvent>>())
            {
                await handler.Handle((TEvent)domainEvent, cancellationToken);
            }
        }

        public override async Task PostCommitAsync(
            IServiceProvider services, IDomainEvent domainEvent, DomainEventDispatcher dispatcher)
        {
            foreach (var handler in services.GetServices<IPostCommitEventHandler<TEvent>>())
            {
                try
                {
                    // The action has committed: a cancelled request must not cancel its side effects
                    await handler.Handle((TEvent)domainEvent, CancellationToken.None);
                }
                // Any failure of a side effect is logged, never surfaced (ADR 0005)
                catch (Exception exception)
                {
                    dispatcher.LogPostCommitFailure(
                        dispatcher.ForLog(exception), handler.GetType().Name, typeof(TEvent).Name);
                }
            }
        }
    }
}
