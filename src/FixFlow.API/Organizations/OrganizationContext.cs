namespace FixFlow.API.Organizations;

/// <summary>The request's organization context (§8, D5), which <see cref="OrganizationContextMiddleware"/> resolves.</summary>
public static class OrganizationContext
{
    /// <summary>
    /// Names the request's organization by its slug. The SPA's HTTP client sends it on every API call, taken only from
    /// the current URL.
    /// </summary>
    public const string HeaderName = "X-Organization";
}
