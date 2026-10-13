using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Context;

namespace FixFlow.Hosting.Logging;

public sealed class RequestLogContextOptions
{
    public string UserIdClaimType { get; set; } = ClaimTypes.NameIdentifier;

    /// <summary>Set by the API once the organization context adds the claim (T1.5).</summary>
    public string? OrganizationIdClaimType { get; set; }
}

/// <summary>
/// Adds the caller's UserId and OrganizationId (IDs only) to every log event written during the request, to the
/// request completion event and to the request's trace.
/// </summary>
public sealed class RequestLogContextMiddleware(
    RequestDelegate next,
    IDiagnosticContext diagnosticContext,
    IOptions<RequestLogContextOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;

        using var userScope = Push("UserId", "fixflow.user_id", FindClaim(user, options.Value.UserIdClaimType));
        using var organizationScope = Push(
            "OrganizationId", "fixflow.organization_id", FindClaim(user, options.Value.OrganizationIdClaimType));

        await next(context);
    }

    private IDisposable? Push(string property, string tag, string? value)
    {
        if (value is null)
        {
            return null;
        }

        // The completion event is written after the LogContext scope ends, so it gets the value separately
        diagnosticContext.Set(property, value);
        Activity.Current?.SetTag(tag, value);
        return LogContext.PushProperty(property, value);
    }

    private static string? FindClaim(ClaimsPrincipal user, string? claimType) =>
        claimType is null ? null : user.FindFirst(claimType)?.Value;
}
