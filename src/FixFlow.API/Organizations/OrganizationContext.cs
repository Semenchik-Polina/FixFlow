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
    /// The authentication type of the identity that the middleware adds to the request's user. <c>HttpCurrentUser</c>
    /// reads the organization and the roles from that identity alone: the cookie's identity also carries the claims
    /// stored with the account, which never decide an organization or a role.
    /// </summary>
    public const string AuthenticationType = "FixFlow.Organization";
}
