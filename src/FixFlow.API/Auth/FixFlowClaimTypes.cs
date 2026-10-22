namespace FixFlow.API.Auth;

public static class FixFlowClaimTypes
{
    /// <summary>
    /// The organization of the request, added by the claims transformation once the <c>X-Organization</c> header
    /// is validated against the caller's active membership (T1.5). Never issued in the auth cookie.
    /// </summary>
    public const string OrganizationId = "fixflow:organization_id";
}
