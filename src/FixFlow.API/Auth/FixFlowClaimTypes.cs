namespace FixFlow.API.Auth;

/// <summary>
/// The claims of the request's organization context. <c>OrganizationContextMiddleware</c> adds them, on an identity of
/// their own, once the <c>X-Organization</c> header is validated against the caller's active membership (§8). They are
/// never issued in the auth cookie: every request resolves them again.
/// </summary>
public static class FixFlowClaimTypes
{
    /// <summary>The organization of the request.</summary>
    public const string OrganizationId = "fixflow:organization_id";

    /// <summary>
    /// One claim per role of the caller's membership, by the role's name. It is the role claim type of the organization
    /// context's identity, so <c>IsInRole</c> and <c>RequireRole</c> see the roles of the request's organization only.
    /// </summary>
    public const string Role = "fixflow:role";
}
