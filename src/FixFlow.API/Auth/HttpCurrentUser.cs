using System.Security.Claims;
using FixFlow.Application.Auth;

namespace FixFlow.API.Auth;

/// <summary>
/// <see cref="ICurrentUser"/> from the request's claims, or from the open <see cref="TrustedOrganizationScope"/> for the
/// organization. Outside a request (startup, migrations) there is no user and no organization.
/// </summary>
/// <remarks>
/// It takes the concrete scope, which <see cref="ITrustedOrganizationScope"/> doesn't expose, to read its open id. It
/// doesn't implement that interface, so casting <see cref="ICurrentUser"/> can't open a scope.
/// </remarks>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor, TrustedOrganizationScope trustedScope)
    : ICurrentUser
{
    // The request this scope serves, taken when the scope creates the service. The accessor reads an AsyncLocal, which
    // work queued without the caller's ExecutionContext doesn't see: HybridCache runs its factories that way, and
    // their queries read the user and the organization through this class (T1.5). The principal is still read on each
    // access, because authentication and the organization context set it after services exist.
    private readonly HttpContext? _httpContext = httpContextAccessor.HttpContext;

    public Guid? UserId => FindId(ClaimTypes.NameIdentifier);

    // A scope opens only when the request has no organization claim, so at most one of the two is set
    public Guid? OrganizationId => trustedScope.OrganizationId ?? FindId(FixFlowClaimTypes.OrganizationId);

    private Guid? FindId(string claimType) =>
        Guid.TryParse(_httpContext?.User.FindFirst(claimType)?.Value, out var id) ? id : null;
}
