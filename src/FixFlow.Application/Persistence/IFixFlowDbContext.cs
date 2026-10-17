using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace FixFlow.Application.Persistence;

/// <summary>
/// The database as handlers see it (plan P13): no repositories, just the DbContext behind an interface, because
/// Application can't reference Infrastructure. Each module adds its <c>DbSet</c>s here as it lands.
/// <see cref="SaveChangesAsync"/> also dispatches the domain events (ADR 0005) and turns a stale row version into
/// <see cref="Errors.ConcurrencyConflictException"/>.
/// </summary>
public interface IFixFlowDbContext
{
    /// <summary>For a command that saves more than once in one transaction (<c>BeginTransactionAsync</c>).</summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
