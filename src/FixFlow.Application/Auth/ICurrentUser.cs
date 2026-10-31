using FixFlow.Domain.Organizations;

namespace FixFlow.Application.Auth;

/// <summary>
/// The caller of the current request. The only source of the active organization and of the caller's roles in it
/// (CLAUDE.md §3.1): handlers, query filters, authorization handlers and persistence read them from here, never from
/// request bodies or route values. The server sets the organization in one of two ways: from the caller's active
/// membership in the organization that the <c>X-Organization</c> header names (§8), or with an
/// <see cref="ITrustedOrganizationScope"/> that a handler opens for an organization it derived itself (plan P25).
/// </summary>
public interface ICurrentUser
{
    /// <summary>Null for an anonymous caller.</summary>
    Guid? UserId { get; }

    /// <summary>
    /// The organization of this request: validated against the caller's active membership, or the open trusted
    /// scope's. Null outside an organization context: organization-owned data is then neither visible nor writable.
    /// </summary>
    Guid? OrganizationId { get; }

    /// <summary>
    /// The caller's roles in <see cref="OrganizationId"/>'s organization: those of their active membership, so at least
    /// one. Empty without an organization context, and while a trusted scope is open: the scope gives an organization,
    /// never roles.
    /// </summary>
    IReadOnlySet<Role> Roles { get; }
}
