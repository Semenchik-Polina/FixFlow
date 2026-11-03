namespace FixFlow.API.RateLimiting;

/// <summary>The names an endpoint passes to <c>RequireRateLimiting</c>.</summary>
public static class RateLimitPolicies
{
    /// <summary>Signup and login (S6, plan P28); T1.4 adds forgot and reset password.</summary>
    public const string Auth = "auth";
}
