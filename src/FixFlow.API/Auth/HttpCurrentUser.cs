using System.Security.Claims;
using FixFlow.Application.Auth;

namespace FixFlow.API.Auth;

/// <summary>
/// <see cref="ICurrentUser"/> from the request's claims. Outside a request (startup, migrations) there is no user and
/// no organization.
/// </summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId => FindId(ClaimTypes.NameIdentifier);

    public Guid? OrganizationId => FindId(FixFlowClaimTypes.OrganizationId);

    private Guid? FindId(string claimType) =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst(claimType)?.Value, out var id) ? id : null;
}
