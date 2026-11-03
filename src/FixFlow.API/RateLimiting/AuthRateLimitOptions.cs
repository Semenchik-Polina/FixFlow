using System.ComponentModel.DataAnnotations;

namespace FixFlow.API.RateLimiting;

/// <summary>
/// The budget of <see cref="RateLimitPolicies.Auth"/>: requests per endpoint and client address in a fixed window
/// (plan P28), from <c>RateLimiting:Auth</c>. The defaults need no setting, so the build-time OpenAPI run has none.
/// </summary>
public sealed class AuthRateLimitOptions
{
    public const string SectionName = "RateLimiting:Auth";

    /// <summary>How many requests a client may send to one endpoint in a window.</summary>
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; } = 10;

    /// <summary>The window's length. It starts at the client's first request to the endpoint.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}
