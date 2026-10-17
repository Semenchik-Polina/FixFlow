using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FixFlow.Infrastructure.Persistence.Interceptors;

/// <summary>
/// The post-commit phase of a command that saves several times in one explicit transaction (ADR 0005): the events of
/// all its saves are dispatched once, after the commit. A rollback or a failed commit drops them, and so does a new
/// transaction (one disposed without commit or rollback leaves no other trace).
/// </summary>
internal sealed class DomainEventsTransactionInterceptor : DbTransactionInterceptor
{
    public override async Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is FixFlowDbContext context)
        {
            await context.DispatchPostCommitAsync();
        }
    }

    /// <summary>A synchronous commit can't run the async post-commit handlers, so it would silently skip them.</summary>
    public override InterceptionResult TransactionCommitting(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
    {
        if (eventData.Context is FixFlowDbContext { HasPendingPostCommitEvents: true })
        {
            throw new NotSupportedException("Use CommitAsync: the transaction has domain events to dispatch.");
        }

        return result;
    }

    public override Task TransactionRolledBackAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData);
        return Task.CompletedTask;
    }

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
        Discard(eventData);

    public override Task TransactionFailedAsync(
        DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData);
        return Task.CompletedTask;
    }

    public override void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData) =>
        Discard(eventData);

    public override ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        Discard(eventData);
        return ValueTask.FromResult(result);
    }

    public override DbTransaction TransactionStarted(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        Discard(eventData);
        return result;
    }

    private static void Discard(DbContextEventData eventData)
    {
        if (eventData.Context is FixFlowDbContext context)
        {
            context.DiscardPostCommitEvents();
        }
    }
}
