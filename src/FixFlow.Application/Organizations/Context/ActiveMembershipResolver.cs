using FixFlow.Application.Auth;
using FixFlow.Application.Persistence;
using FixFlow.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace FixFlow.Application.Organizations.Context;

/// <summary>
/// Finds the caller's active membership in the organization a request names by its slug, which gives the request its
/// organization context (§8). Cached per user and slug (<see cref="MembershipCache"/>); a change to any of the user's
/// memberships drops their entries after its commit, so a suspension applies from the next request on.
/// </summary>
public sealed class ActiveMembershipResolver(IFixFlowDbContext db, ICurrentUser currentUser, HybridCache cache)
{
    /// <returns>
    /// The membership; or null when the slug isn't valid, no organization has it, or the caller isn't an active member
    /// of it. A miss is cached like a membership.
    /// </returns>
    /// <exception cref="InvalidOperationException">No user is signed in.</exception>
    public async ValueTask<ActiveMembership?> FindAsync(string slug, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slug);
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Only a signed-in user has memberships.");

        // No organization can have an invalid slug, so it costs neither a query nor a cache entry
        if (!OrganizationSlug.IsValid(slug, out _))
        {
            return null;
        }

        return await cache.GetOrCreateAsync(
            MembershipCache.Key(userId, slug),
            (db, slug),
            static (state, ct) => LoadAsync(state.db, state.slug, ct),
            MembershipCache.EntryOptions,
            [MembershipCache.Tag(userId)],
            cancellationToken);
    }

    private static async ValueTask<ActiveMembership?> LoadAsync(
        IFixFlowDbContext db, string slug, CancellationToken cancellationToken)
    {
        // The caller's own rows, with the organization filter off: see OwnMemberships for why that is safe
        var membership = await db.OwnMemberships
            .AsNoTracking()
            .Where(membership => membership.Status == MembershipStatus.Active)
            .Join(
                db.Organizations.Where(organization => organization.Slug == slug),
                membership => membership.OrganizationId,
                organization => organization.Id,
                (membership, organization) => new
                {
                    organization.Id,
                    Roles = membership.Roles.Select(role => role.Role).ToList(),
                })
            .SingleOrDefaultAsync(cancellationToken);

        // Roles are stored by name, so SQL would sort them alphabetically; the enum's order is the meaningful one
        return membership is null ? null : new ActiveMembership(membership.Id, [.. membership.Roles.Order()]);
    }
}
