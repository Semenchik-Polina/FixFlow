using System.Collections.Concurrent;
using FixFlow.Application.Errors;
using FixFlow.Application.Events;
using FixFlow.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FixFlow.Infrastructure.Persistence.Events;

/// <summary>
/// Calls the handlers of a domain event (ADR 0005, plan P12). Handlers come from the request's scope, so
/// in-transaction handlers change aggregates tracked by the same DbContext that is saving.
/// </summary>
public sealed partial class DomainEventDispatcher(
    IServiceProvider services,
    IExceptionRedactor redactor,
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

    // Without messages outside Development: they can hold personal data (ADR 0007)
    private Exception ForLog(Exception exception) => redactor.ForLog(exception);

    // Types and IDs only: the exception carries the details, and the event's data stays out of the logs (S26)
    [LoggerMessage(Level = LogLevel.Error, Message = "Post-commit handler {Handler} failed for {DomainEvent}")]
    private partial void LogPostCommitFailure(Exception exception, string handler, string domainEvent);

    [LoggerMessage(Level = LogLevel.Error, Message = "Post-commit handlers for {DomainEvent} could not be created")]
    private partial void LogPostCommitHandlersUnavailable(Exception exception, string domainEvent);

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
            IPostCommitEventHandler<TEvent>[] handlers;
            try
            {
                handlers = [.. services.GetServices<IPostCommitEventHandler<TEvent>>()];
            }
            // The container creates them all at once, so one that can't be created leaves none of them to run. The
            // action has committed all the same: logged, never surfaced (ADR 0005)
            catch (Exception exception)
            {
                dispatcher.LogPostCommitHandlersUnavailable(dispatcher.ForLog(exception), typeof(TEvent).Name);
                return;
            }

            foreach (var handler in handlers)
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
