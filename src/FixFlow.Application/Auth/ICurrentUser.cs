namespace FixFlow.Application.Auth;

/// <summary>
/// The caller of the current request. The only source of the active organization (CLAUDE.md §3.1): handlers, query
/// filters and persistence read it from here, never from request bodies or route values. The server sets it in one of
/// two ways: from the caller's active membership (T1.5), or with an <see cref="ITrustedOrganizationScope"/> that a
/// handler opens for an organization it derived itself (plan P25). T1.5 adds the roles of the active membership.
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
}
