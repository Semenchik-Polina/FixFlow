using FixFlow.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace FixFlow.Application.Persistence;

/// <summary>
/// The database as handlers see it (plan P13): no repositories, just the DbContext behind an interface, because
/// Application can't reference Infrastructure. Each module adds its <c>DbSet</c>s here as it lands.
/// <see cref="SaveChangesAsync"/> also dispatches the domain events (ADR 0005) and turns a stale row version into
/// <see cref="Errors.ConcurrencyConflictException"/>.
/// </summary>
/// <remarks>
/// Every set here is organization-owned, or global by design and on a short list (plan P22). There is no set of
/// <see cref="Identity.User"/>: users have no organization filter, so they are reached through queries scoped to the
/// organization (or the caller's own account), which the slices that need them add. A model test checks this.
/// </remarks>
public interface IFixFlowDbContext
{
    /// <summary>Every organization: they are the boundary, found by slug before any organization context exists.</summary>
    DbSet<Organization> Organizations { get; }

    /// <summary>The request's organization's memberships only (organization filter), each with its roles.</summary>
    DbSet<OrganizationMembership> OrganizationMemberships { get; }

    /// <summary>For a command that saves more than once in one transaction (<c>BeginTransactionAsync</c>).</summary>
    DatabaseFacade Database { get; }

    /// <summary>Once a save throws, the unit of work is over: saving again throws. Retry in a new request.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
