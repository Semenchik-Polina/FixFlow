namespace FixFlow.Application.Auth;

/// <summary>
/// An organization context that the server opens itself, for a caller who has none (plan P25). Signup and
/// <c>POST /organizations</c> open it for the organization they have just created, and invite acceptance (T3.2) for the
/// invite's organization. While it is open, <see cref="ICurrentUser.OrganizationId"/> returns its id, so the ownership
/// interceptor, the query filters and the audit stamps work unchanged.
/// </summary>
/// <remarks>
/// <para>
/// Open it only for an id the server derived itself, never for one from the request (a body, a route value or a
/// header): that would let a caller write into any organization (CLAUDE.md §3.1).
/// </para>
/// <para>
/// Only the handlers on the allow-list of <c>ArchitectureTests</c> take it. That test sees constructors only, so never
/// resolve it from an <see cref="IServiceProvider"/>.
/// </para>
/// </remarks>
public interface ITrustedOrganizationScope
{
    /// <summary>
    /// Opens the scope until the returned object is disposed. Keep it open through the commit, so the post-commit
    /// handlers run in the organization too.
    /// </summary>
    /// <param name="organizationId">An organization the server derived itself, never one from the request.</param>
    /// <exception cref="InvalidOperationException">
    /// The id is empty, a scope is already open, or the caller already has an organization context.
    /// </exception>
    IDisposable Open(Guid organizationId);
}
