using FixFlow.Application.Identity;
using FixFlow.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace FixFlow.Application.Persistence;

/// <summary>
/// The database as handlers see it (plan P13): no repositories, just the DbContext behind an interface, because
/// Application can't reference Infrastructure. Each module adds its <c>DbSet</c>s here as it lands.
/// <see cref="SaveChangesAsync"/> also dispatches the domain events (ADR 0005), and turns a stale row version into
/// <see cref="Errors.ConcurrencyConflictException"/> and a unique violation into
/// <see cref="Errors.UniqueConstraintViolationException"/>.
/// </summary>
/// <remarks>
/// Every set here is organization-owned, or global by design and on a short list (plan P22). There is no set of
/// <see cref="Identity.User"/>: users have no organization filter, so they are reached through queries scoped to the
/// organization, or to the caller (<see cref="OwnAccount"/>). Every query that isn't a set is on a named list with its
/// reason. A model test checks both.
/// </remarks>
public interface IFixFlowDbContext
{
    /// <summary>Every organization: they are the boundary, found by slug before any organization context exists.</summary>
    DbSet<Organization> Organizations { get; }

    /// <summary>The request's organization's memberships only (organization filter), each with its roles.</summary>
    DbSet<OrganizationMembership> OrganizationMemberships { get; }

    /// <summary>The caller's own account: one row when signed in, none for an anonymous caller.</summary>
    IQueryable<User> OwnAccount { get; }

    /// <summary>
    /// The caller's own memberships in every organization, active or not, each with its roles; none for an anonymous
    /// caller. The one query without the organization filter: a user's memberships decide which organizations they
    /// may enter, so they are read before any organization context exists (<c>/me</c>, T1.5).
    /// </summary>
    IQueryable<OrganizationMembership> OwnMemberships { get; }

    /// <summary>For a command that saves more than once in one transaction (<c>BeginTransactionAsync</c>).</summary>
    DatabaseFacade Database { get; }

    /// <summary>Once a save throws, the unit of work is over: saving again throws. Retry in a new request.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
