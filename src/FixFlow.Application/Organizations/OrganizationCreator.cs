using FixFlow.Application.Auth;
using FixFlow.Application.Errors;
using FixFlow.Application.Persistence;
using FixFlow.Application.Results;
using FixFlow.Domain.Organizations;
using NodaTime;

namespace FixFlow.Application.Organizations;

/// <summary>
/// Creates an organization: the organization and its founder's Admin membership, in one save. Signup and
/// <c>POST /organizations</c> share it.
/// </summary>
internal sealed class OrganizationCreator(IFixFlowDbContext db, ICurrentUser currentUser, IClock clock)
{
    /// <summary>
    /// Runs inside the trusted scope that the caller opened for <paramref name="organization"/>
    /// (<see cref="ITrustedOrganizationScope"/>), which stamps the membership with the organization. It doesn't open
    /// the scope itself: the caller keeps it open through its commit, so the post-commit handlers run in the
    /// organization too.
    /// </summary>
    /// <param name="organization">New, and not saved yet.</param>
    /// <param name="founderId">The user who becomes the organization's first Admin.</param>
    /// <returns>Success, or a field error on <c>slug</c> when the slug is taken.</returns>
    /// <exception cref="InvalidOperationException">
    /// The organization context isn't <paramref name="organization"/>'s.
    /// </exception>
    public async Task<Result> CreateAsync(Organization organization, Guid founderId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(organization);

        if (currentUser.OrganizationId != organization.Id)
        {
            throw new InvalidOperationException(
                "Creating an organization needs the trusted organization scope of that organization.");
        }

        db.Organizations.Add(organization);
        db.OrganizationMemberships.Add(
            OrganizationMembership.Create(founderId, [Role.Admin], clock.GetCurrentInstant()));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        // The save failed, so the unit of work is over: the caller returns this and saves nothing more
        catch (UniqueConstraintViolationException exception)
            when (exception.ConstraintName == UniqueIndexes.OrganizationSlug)
        {
            return Result.FieldError("slug", "This slug is already taken.");
        }

        return Result.Success();
    }
}
