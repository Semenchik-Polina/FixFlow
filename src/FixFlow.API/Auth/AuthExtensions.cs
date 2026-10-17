using FixFlow.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace FixFlow.API.Auth;

public static class AuthExtensions
{
    /// <summary>
    /// The Identity cookie schemes, the fallback policy and <see cref="ICurrentUser"/>: every endpoint needs a
    /// signed-in user unless it is allow-listed with <c>AllowAnonymous</c> (S7). Unknown routes too, so anonymous
    /// callers can't probe for them.
    /// T1.2 adds the Identity services and the S1 cookie settings on top: with <c>AddIdentityCore</c>, not
    /// <c>AddIdentity</c>, which would register these schemes a second time.
    /// </summary>
    public static IServiceCollection AddFixFlowAuth(this IServiceCollection services)
    {
        services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        // An API answers 401/403 itself; the SPA handles the login page
        services.ConfigureApplicationCookie(options =>
        {
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        return services;
    }
}
