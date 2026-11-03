using FixFlow.Domain.Organizations;
using Microsoft.AspNetCore.Authorization;

namespace FixFlow.API.Auth;

/// <summary>
/// The caller has at least one of <see cref="Roles"/> in the request's organization. The requirement of every
/// <see cref="Policies"/> entry; <see cref="OrganizationRoleHandler"/> checks it.
/// </summary>
public sealed class OrganizationRoleRequirement(IReadOnlySet<Role> roles) : IAuthorizationRequirement
{
    public IReadOnlySet<Role> Roles { get; } = roles;

    // Authorization logs: role names, no personal data
    public override string ToString() => $"One of the roles {string.Join(", ", Roles.Order())} in the organization";
}
