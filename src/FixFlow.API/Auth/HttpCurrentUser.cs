using System.Collections.Frozen;
using System.Security.Claims;
using FixFlow.Application.Auth;
using FixFlow.Domain.Organizations;

namespace FixFlow.API.Auth;

/// <summary>
/// <see cref="ICurrentUser"/> from the request's claims, or from the open <see cref="TrustedOrganizationScope"/> for the
/// organization. Outside a request (startup, migrations) there is no user and no organization.
/// </summary>
/// <remarks>
/// <para>
/// The scope has an id only while a handler holds it open: one that writes an organization's first rows before any
/// organization context exists. Signup and <c>POST /organizations</c> open it for the organization they have just
/// created, and invite acceptance (T3.2) will open it for the invite's organization (the allow-list of
/// <c>ArchitectureTests</c> names them). Their endpoints are organization-less, so the request has no organization
/// claim, and the scope stays open through the commit, so the post-commit handlers run in it too. In every other
/// request its id is null, and the organization and roles come from the organization context's claims.
/// </para>
/// <para>
/// It takes the concrete scope, which <see cref="ITrustedOrganizationScope"/> doesn't expose, to read its open id. It
/// doesn't implement that interface, so casting <see cref="ICurrentUser"/> can't open a scope.
/// </para>
/// </remarks>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor, TrustedOrganizationScope trustedScope)
    : ICurrentUser, IDisposable
{
    private static readonly FrozenDictionary<string, Role> _rolesByName =
        Enum.GetValues<Role>().ToFrozenDictionary(role => role.ToString());

    // The request this scope serves, taken when the scope creates the service. The accessor reads an AsyncLocal, which
    // work queued without the caller's ExecutionContext doesn't see: HybridCache runs its factories that way when the
    // caller can cancel, and their queries read the user and the organization through this class (T1.5). The
    // principal is still read on each access, because authentication and the organization context set it after
    // services exist. Dropped when the scope ends with the request (Dispose).
    private HttpContext? _httpContext = httpContextAccessor.HttpContext;

    public Guid? UserId => FindId(ClaimTypes.NameIdentifier);

    // From the open scope (signup, POST /organizations) or from the organization context's claim. A scope opens only
    // when the request has no organization claim, so at most one of the two is set.
    public Guid? OrganizationId => trustedScope.OrganizationId ?? FindId(FixFlowClaimTypes.OrganizationId);

    // A scope id means a handler holds the scope open. The scope gives an organization only, never roles (plan P25):
    // the caller has no membership there yet, or has just created it, so no role check passes inside it. Otherwise
    // the roles are the organization context's claims, and none without a context.
    public IReadOnlySet<Role> Roles => trustedScope.OrganizationId is null ? FindRoles() : FrozenSet<Role>.Empty;

    private Guid? FindId(string claimType) =>
        Guid.TryParse(_httpContext?.User.FindFirst(claimType)?.Value, out var id) ? id : null;

    private HashSet<Role> FindRoles()
    {
        var roles = new HashSet<Role>();
        foreach (var claim in _httpContext?.User.FindAll(FixFlowClaimTypes.Role) ?? [])
        {
            // The organization context writes the enum's names; anything else (a number, flags, another case) grants
            // nothing
            if (_rolesByName.TryGetValue(claim.Value, out var role))
            {
                roles.Add(role);
            }
        }

        return roles;
    }

    // The request's scope ends with the request, before the server reuses its HttpContext for the connection's next
    // one, which may be another user's. Anything that still holds this service sees no user and no organization.
    public void Dispose() => _httpContext = null;
}
