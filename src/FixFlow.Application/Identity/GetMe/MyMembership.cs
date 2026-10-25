using FixFlow.Domain.Organizations;

namespace FixFlow.Application.Identity.GetMe;

/// <summary>One of the caller's active memberships: the SPA sends <see cref="Slug"/> as <c>X-Organization</c>.</summary>
/// <param name="Roles">The caller's roles in this organization, in the order of <see cref="Role"/>.</param>
public sealed record MyMembership(
    Guid OrganizationId,
    string OrganizationName,
    string Slug,
    IReadOnlyList<Role> Roles);
