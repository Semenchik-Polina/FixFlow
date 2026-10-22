using System.Collections.Concurrent;
using FixFlow.Application.Events;
using FixFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.API.IntegrationTests.Persistence.TestModel;

// One event type per dispatch scenario, with its handlers. PersistenceTestDatabase registers them from this assembly.

/// <summary>In-transaction: bumps another widget, like a work order closing its request.</summary>
public sealed record WidgetBumpRequested(Guid TargetId) : IDomainEvent;

/// <summary>In-transaction: the handler breaks a business rule.</summary>
public sealed record WidgetRejected : IDomainEvent
{
    public const string Message = "This widget can't be saved.";
}

/// <summary>In-transaction: the handler raises the same event again, forever.</summary>
public sealed record WidgetEchoed(Guid WidgetId) : IDomainEvent;

/// <summary>Post-commit: recorded by <see cref="PostCommitRecorder"/>.</summary>
public sealed record WidgetNoticed(Guid WidgetId) : IDomainEvent;

/// <summary>Post-commit: one handler fails, the other still records it.</summary>
public sealed record WidgetNoticeFailed(Guid WidgetId) : IDomainEvent;

/// <summary>Post-commit: the handler throws what <see cref="Create"/> returns (how failures are logged).</summary>
public sealed record WidgetSideEffectThrew(Func<Exception> Create) : IDomainEvent;

internal sealed class BumpTargetHandler(TestDbContext db) : IInTransactionEventHandler<WidgetBumpRequested>
{
    public async Task Handle(WidgetBumpRequested domainEvent, CancellationToken cancellationToken)
    {
        var target = await db.Widgets.SingleAsync(widget => widget.Id == domainEvent.TargetId, cancellationToken);
        target.Bump();
    }
}

internal sealed class RejectHandler : IInTransactionEventHandler<WidgetRejected>
{
    public Task Handle(WidgetRejected domainEvent, CancellationToken cancellationToken) =>
        throw new DomainException(WidgetRejected.Message);
}

internal sealed class EchoHandler(TestDbContext db) : IInTransactionEventHandler<WidgetEchoed>
{
    public async Task Handle(WidgetEchoed domainEvent, CancellationToken cancellationToken)
    {
        var widget = await db.Widgets.SingleAsync(widget => widget.Id == domainEvent.WidgetId, cancellationToken);
        widget.Publish(new WidgetEchoed(widget.Id));
    }
}

internal sealed class RecordNoticeHandler(PostCommitRecorder recorder)
    : IPostCommitEventHandler<WidgetNoticed>, IPostCommitEventHandler<WidgetNoticeFailed>
{
    public Task Handle(WidgetNoticed domainEvent, CancellationToken cancellationToken)
    {
        recorder.Record(domainEvent.WidgetId);
        return Task.CompletedTask;
    }

    public Task Handle(WidgetNoticeFailed domainEvent, CancellationToken cancellationToken)
    {
        recorder.Record(domainEvent.WidgetId);
        return Task.CompletedTask;
    }
}

internal sealed class FailingNoticeHandler : IPostCommitEventHandler<WidgetNoticeFailed>
{
    // Like an SMTP error, the message quotes an address: it must stay out of the logs
    public Task Handle(WidgetNoticeFailed domainEvent, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("550 5.1.1 <jane.doe@example.test>: Recipient address rejected");
}

internal sealed class ThrowingSideEffectHandler : IPostCommitEventHandler<WidgetSideEffectThrew>
{
    public Task Handle(WidgetSideEffectThrew domainEvent, CancellationToken cancellationToken) =>
        throw domainEvent.Create();
}

/// <summary>How often the post-commit handlers saw each widget. Shared by parallel tests: read only your own ids.</summary>
public sealed class PostCommitRecorder
{
    private readonly ConcurrentDictionary<Guid, int> _counts = new();

    public void Record(Guid widgetId) => _counts.AddOrUpdate(widgetId, 1, (_, count) => count + 1);

    public int Count(Guid widgetId) => _counts.GetValueOrDefault(widgetId);
}
