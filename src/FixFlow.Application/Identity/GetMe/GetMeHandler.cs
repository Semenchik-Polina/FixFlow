using FixFlow.Application.Persistence;
using FixFlow.Application.Results;
using FixFlow.Domain.Organizations;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.Application.Identity.GetMe;

/// <summary>
/// Reads the caller's own rows only (<see cref="IFixFlowDbContext.OwnAccount"/>,
/// <see cref="IFixFlowDbContext.OwnMemberships"/>), before any organization context exists.
/// </summary>
internal sealed class GetMeHandler(IFixFlowDbContext db) : IRequestHandler<GetMeQuery, Result<MeResponse>>
{
    public async Task<Result<MeResponse>> Handle(GetMeQuery query, CancellationToken cancellationToken)
    {
        var profile = await db.OwnAccount
            .AsNoTracking()
            .Select(user => new { user.Id, Email = user.Email!, user.FirstName, user.LastName })
            .SingleOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            return Result.NotFound();
        }

        var memberships = await db.OwnMemberships
            .AsNoTracking()
            .Where(membership => membership.Status == MembershipStatus.Active)
            .Join(
                db.Organizations,
                membership => membership.OrganizationId,
                organization => organization.Id,
                (membership, organization) => new { membership, organization })
            // The slug breaks ties: names aren't unique
            .OrderBy(row => row.organization.Name)
            .ThenBy(row => row.organization.Slug)
            .Select(row => new
            {
                row.organization.Id,
                row.organization.Name,
                row.organization.Slug,
                Roles = row.membership.Roles.Select(role => role.Role).ToList(),
            })
            .ToListAsync(cancellationToken);

        return new MeResponse(
            profile.Id,
            profile.Email,
            profile.FirstName,
            profile.LastName,
            // Roles are stored by name, so SQL would sort them alphabetically; the enum's order is the meaningful one
            [.. memberships.Select(m => new MyMembership(m.Id, m.Name, m.Slug, [.. m.Roles.Order()]))]);
    }
}
