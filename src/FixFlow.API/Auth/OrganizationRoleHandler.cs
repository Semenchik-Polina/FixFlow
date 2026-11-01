using FixFlow.Application.Auth;
using Microsoft.AspNetCore.Authorization;

namespace FixFlow.API.Auth;

/// <summary>
/// Checks an <see cref="OrganizationRoleRequirement"/> against <see cref="ICurrentUser.Roles"/>, the only source of the
/// caller's roles (CLAUDE.md §3.1), not against the principal: no <c>IsInRole</c>, which would also see role claims on
/// the cookie's identity. Without an organization context there are no roles, so a role policy on an organization-less
/// endpoint fails closed. Scoped, like the current user it reads.
/// </summary>
internal sealed class OrganizationRoleHandler(ICurrentUser currentUser) : AuthorizationHandler<OrganizationRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, OrganizationRoleRequirement requirement)
    {
        if (currentUser.OrganizationId is not null && currentUser.Roles.Overlaps(requirement.Roles))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
