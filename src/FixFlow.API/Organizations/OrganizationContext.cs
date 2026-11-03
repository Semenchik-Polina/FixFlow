using System.Security.Claims;

namespace FixFlow.API.Organizations;

/// <summary>The request's organization context (§8, D5), which <see cref="OrganizationContextMiddleware"/> resolves.</summary>
public static class OrganizationContext
{
    /// <summary>
    /// Names the request's organization by its slug. The SPA's HTTP client sends it on every API call, taken only from
    /// the current URL.
    /// </summary>
    public const string HeaderName = "X-Organization";

    /// <summary>
    /// The authentication type of the identity that the middleware adds to the request's user. The organization and the
    /// roles are read from that identity alone (<see cref="FindIdentity"/>): the cookie's identity also carries the
    /// claims stored with the account, which never decide an organization or a role.
    /// </summary>
    public const string AuthenticationType = "FixFlow.Organization";

    /// <summary>
    /// The organization context's identity on <paramref name="user"/>, the one place that finds it: for the current
    /// user, the trusted scope's guard and the request's logs. Null without one, and with several, which the middleware
    /// never adds: a second one would be stale or foreign, so neither decides.
    /// </summary>
    public static ClaimsIdentity? FindIdentity(ClaimsPrincipal? user) =>
        Identities(user).ToList() is [var single] ? single : null;

    /// <summary>Whether <paramref name="user"/> has any identity of the context's type, a duplicate included.</summary>
    public static bool HasAnyIdentity(ClaimsPrincipal? user) => Identities(user).Any();

    private static IEnumerable<ClaimsIdentity> Identities(ClaimsPrincipal? user) =>
        user?.Identities.Where(identity => identity.AuthenticationType == AuthenticationType) ?? [];
}
