using System.Globalization;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.IntegrationTests.Persistence.TestModel;
using FixFlow.Domain;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;

namespace FixFlow.API.IntegrationTests.Persistence;

/// <summary>Two-phase dispatch during SaveChanges (ADR 0005).</summary>
public class DomainEventDispatchTests(FixFlowApiFactory api)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly TestCurrentUser _user = TestCurrentUser.InNewOrganization();

    [Fact]
    public async Task InTransactionHandler_ChangeToAnotherAggregate_CommitsWithTheTrigger()
    {
        var (trigger, target) = await SaveWidgetsAsync();

        await using (var scope = api.Persistence.CreateScope(_user))
        {
            var saved = await scope.Db.Widgets.SingleAsync(w => w.Id == trigger.Id, _ct);
            saved.Rename("Renamed");
            saved.Publish(new WidgetBumpRequested(target.Id));
            await scope.Db.SaveChangesAsync(_ct);
        }

        Assert.Equal("Renamed", (await LoadAsync(trigger.Id)).Name);
        Assert.Equal(1, (await LoadAsync(target.Id)).Bumps);
    }

    [Fact]
    public async Task InTransactionHandler_Fails_NeitherChangeIsSaved()
    {
        var (trigger, target) = await SaveWidgetsAsync();

        await using (var scope = api.Persistence.CreateScope(_user))
        {
            var saved = await scope.Db.Widgets.SingleAsync(w => w.Id == trigger.Id, _ct);
            saved.Rename("Renamed");
            saved.Publish(new WidgetBumpRequested(target.Id));
            saved.Publish(new WidgetRejected());

            var error = await Assert.ThrowsAsync<DomainException>(() => scope.Db.SaveChangesAsync(_ct));
            Assert.Equal(WidgetRejected.Message, error.Message);
        }

        Assert.Equal(trigger.Name, (await LoadAsync(trigger.Id)).Name);
        Assert.Equal(0, (await LoadAsync(target.Id)).Bumps);
    }

    [Fact]
    public async Task InTransactionHandlers_RaisingEventsForever_Throw()
    {
        var (trigger, _) = await SaveWidgetsAsync();

        await using (var scope = api.Persistence.CreateScope(_user))
        {
            var saved = await scope.Db.Widgets.SingleAsync(w => w.Id == trigger.Id, _ct);
            saved.Rename("Renamed");
            saved.Publish(new WidgetEchoed(saved.Id));

            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Db.SaveChangesAsync(_ct));
        }

        Assert.Equal(trigger.Name, (await LoadAsync(trigger.Id)).Name);
    }

    [Fact]
    public async Task PostCommitHandler_Fails_ChangeIsSavedAndFailureLogged()
    {
        var (trigger, _) = await SaveWidgetsAsync();

        await using (var scope = api.Persistence.CreateScope(_user))
        {
            var saved = await scope.Db.Widgets.SingleAsync(w => w.Id == trigger.Id, _ct);
            saved.Rename("Renamed");
            saved.Publish(new WidgetNoticeFailed(saved.Id));

            // Doesn't throw: the action stands
            await scope.Db.SaveChangesAsync(_ct);
        }

        Assert.Equal("Renamed", (await LoadAsync(trigger.Id)).Name);
        // The other handler of the event still ran
        Assert.Equal(1, api.Persistence.PostCommit.Count(trigger.Id));
        // WidgetNoticeFailed is raised by this test only
        var failure = Assert.Single(api.Persistence.Logs.Events, logEvent =>
            logEvent.Level == LogEventLevel.Error
            && HasProperty(logEvent, "DomainEvent", nameof(WidgetNoticeFailed))
            && HasProperty(logEvent, "Handler", nameof(FailingNoticeHandler)));
        // Its type and stack trace, but not its message: it quotes an email address (S26, ADR 0007)
        var exception = failure.Exception?.ToString() ?? "";
        Assert.StartsWith("System.InvalidOperationException: [message removed]", exception, StringComparison.Ordinal);
        Assert.Contains(
            $"{nameof(FailingNoticeHandler)}.{nameof(FailingNoticeHandler.Handle)}",
            exception,
            StringComparison.Ordinal);
        Assert.DoesNotContain("example.test", Render(failure), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostCommitHandler_CannotBeCreated_ChangeIsSavedAndLaterEventsDispatched()
    {
        var (trigger, _) = await SaveWidgetsAsync();

        await using (var scope = api.Persistence.CreateScope(_user))
        {
            var saved = await scope.Db.Widgets.SingleAsync(w => w.Id == trigger.Id, _ct);
            saved.Rename("Renamed");
            saved.Publish(new WidgetNoticeMisconfigured(saved.Id));
            saved.Publish(new WidgetNoticed(saved.Id));

            // Doesn't throw: the action stands
            await scope.Db.SaveChangesAsync(_ct);
        }

        Assert.Equal("Renamed", (await LoadAsync(trigger.Id)).Name);
        Assert.Equal(1, api.Persistence.PostCommit.Count(trigger.Id));
        // WidgetNoticeMisconfigured is raised by this test only
        var failure = Assert.Single(api.Persistence.Logs.Events, logEvent =>
            logEvent.Level == LogEventLevel.Error
            && HasProperty(logEvent, "DomainEvent", nameof(WidgetNoticeMisconfigured)));
        // Where it failed, without the message
        Assert.Contains(
            $"{nameof(MisconfiguredNoticeHandler)}..ctor", failure.Exception?.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Smtp:Host", Render(failure), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostCommitHandler_NoExplicitTransaction_RunsOnceAfterSave()
    {
        var (trigger, _) = await SaveWidgetsAsync();

        await using (var scope = api.Persistence.CreateScope(_user))
        {
            var saved = await scope.Db.Widgets.SingleAsync(w => w.Id == trigger.Id, _ct);
            saved.Publish(new WidgetNoticed(saved.Id));
            await scope.Db.SaveChangesAsync(_ct);
        }

        Assert.Equal(1, api.Persistence.PostCommit.Count(trigger.Id));
    }

    [Fact]
    public async Task PostCommitHandlers_ExplicitTransaction_RunOnceAfterCommit()
    {
        var first = new Widget("First");
        var second = new Widget("Second");
        await using var scope = api.Persistence.CreateScope(_user);
        await using var transaction = await scope.Db.Database.BeginTransactionAsync(_ct);

        first.Publish(new WidgetNoticed(first.Id));
        scope.Db.Widgets.Add(first);
        await scope.Db.SaveChangesAsync(_ct);
        second.Publish(new WidgetNoticed(second.Id));
        scope.Db.Widgets.Add(second);
        await scope.Db.SaveChangesAsync(_ct);

        Assert.Equal(0, api.Persistence.PostCommit.Count(first.Id));
        Assert.Equal(0, api.Persistence.PostCommit.Count(second.Id));

        await transaction.CommitAsync(_ct);

        Assert.Equal(1, api.Persistence.PostCommit.Count(first.Id));
        Assert.Equal(1, api.Persistence.PostCommit.Count(second.Id));
    }

    [Fact]
    public async Task PostCommitHandlers_TransactionRolledBack_DoNotRun()
    {
        var widget = new Widget("First");
        await using var scope = api.Persistence.CreateScope(_user);
        await using var transaction = await scope.Db.Database.BeginTransactionAsync(_ct);

        widget.Publish(new WidgetNoticed(widget.Id));
        scope.Db.Widgets.Add(widget);
        await scope.Db.SaveChangesAsync(_ct);
        await transaction.RollbackAsync(_ct);

        Assert.Equal(0, api.Persistence.PostCommit.Count(widget.Id));
    }

    [Fact]
    public async Task PostCommitHandlers_TransactionDisposedWithoutCommit_DoNotRunOnTheNextSave()
    {
        var rolledBack = new Widget("Rolled back");
        var saved = new Widget("Saved");
        await using var scope = api.Persistence.CreateScope(_user);

        await using (var transaction = await scope.Db.Database.BeginTransactionAsync(_ct))
        {
            rolledBack.Publish(new WidgetNoticed(rolledBack.Id));
            scope.Db.Widgets.Add(rolledBack);
            await scope.Db.SaveChangesAsync(_ct);

            // Disposed without commit or rollback, as on an early return
        }

        // A single insert: EF Core starts no transaction for it, so the interceptor sees nothing
        saved.Publish(new WidgetNoticed(saved.Id));
        scope.Db.Widgets.Add(saved);
        await scope.Db.SaveChangesAsync(_ct);

        Assert.Equal(0, api.Persistence.PostCommit.Count(rolledBack.Id));
        Assert.Equal(1, api.Persistence.PostCommit.Count(saved.Id));
    }

    [Fact]
    public async Task SaveChanges_AfterAFailedSave_Throws()
    {
        var (trigger, target) = await SaveWidgetsAsync();

        await using (var scope = api.Persistence.CreateScope(_user))
        {
            var saved = await scope.Db.Widgets.SingleAsync(w => w.Id == trigger.Id, _ct);
            saved.Publish(new WidgetBumpRequested(target.Id));
            saved.Publish(new WidgetRejected());
            await Assert.ThrowsAsync<DomainException>(() => scope.Db.SaveChangesAsync(_ct));

            // The bump is still tracked, but its event is gone: saving it now would skip the post-commit phase
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Db.SaveChangesAsync(_ct));
        }

        Assert.Equal(0, (await LoadAsync(target.Id)).Bumps);
    }

    /// <summary>
    /// Other tests share the sink, and their failed saves leave EF Core's own errors in it, without these properties.
    /// </summary>
    private static bool HasProperty(LogEvent logEvent, string name, string value) =>
        logEvent.Properties.TryGetValue(name, out var property)
        && property is ScalarValue { Value: string actual }
        && actual == value;

    /// <summary>All a sink can write: the message, every property and the exception.</summary>
    private static string Render(LogEvent logEvent) =>
        string.Join(
            Environment.NewLine,
            [
                logEvent.RenderMessage(CultureInfo.InvariantCulture),
                .. logEvent.Properties.Select(property => property.ToString()),
                logEvent.Exception?.ToString(),
            ]);

    private async Task<(Widget Trigger, Widget Target)> SaveWidgetsAsync()
    {
        var trigger = new Widget("Trigger");
        var target = new Widget("Target");
        await using var scope = api.Persistence.CreateScope(_user);
        scope.Db.Widgets.AddRange(trigger, target);
        await scope.Db.SaveChangesAsync(_ct);
        return (trigger, target);
    }

    private async Task<Widget> LoadAsync(Guid widgetId)
    {
        await using var scope = api.Persistence.CreateScope(_user);
        return await scope.Db.Widgets.AsNoTracking().SingleAsync(w => w.Id == widgetId, _ct);
    }
}
