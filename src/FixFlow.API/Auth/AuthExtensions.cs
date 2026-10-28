using FixFlow.Application.Auth;
using FixFlow.Application.Identity;
using FixFlow.Hosting.Logging;
using FixFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FixFlow.API.Auth;

public static class AuthExtensions
{
    /// <summary>
    /// Identity, its cookie schemes, the fallback policy and <see cref="ICurrentUser"/>: every endpoint needs a
    /// signed-in user unless it is allow-listed with <c>AllowAnonymous</c> (S7). Unknown routes too, so anonymous
    /// callers can't probe for them. Identity comes with <c>AddIdentityCore</c>, not <c>AddIdentity</c>, which would
    /// register the cookie schemes a second time (plan P10).
    /// </summary>
    public static IServiceCollection AddFixFlowAuth(this IServiceCollection services)
    {
        services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        // The sign-in manager also registers the security stamp validator, which the application cookie calls on every
        // request that carries one
        services
            .AddFixFlowIdentity()
            .AddSignInManager()
            .AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProvider.Name);
        services.Configure<IdentityOptions>(
            options => options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProvider.Name);
        // Compares the cookie's stamp with the user's on every request (plan T1.4 D1), so a password reset or change
        // ends every other session on its next request. Costs a user lookup per request; a matching stamp leaves the
        // cookie as it is (NonRenewingSecurityStampValidator).
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        services.Replace(ServiceDescriptor.Scoped<ISecurityStampValidator, NonRenewingSecurityStampValidator>());
        services.AddScoped<IAuthSession, SignInManagerAuthSession>();

        services.ConfigureApplicationCookie(options =>
        {
            // S1: no script access, https only, and not sent on cross-site requests other than top-level navigation
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;

            // An API answers 401/403 itself; the SPA handles the login page
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
        // One scope object per request, shared by the handler that opens it and the current user that reads it
        services.AddScoped<TrustedOrganizationScope>();
        services.AddScoped<ITrustedOrganizationScope>(provider => provider.GetRequiredService<TrustedOrganizationScope>());
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        // The organization's id joins the request's logs and trace as soon as T1.5 adds the claim
        services.Configure<RequestLogContextOptions>(
            options => options.OrganizationIdClaimType = FixFlowClaimTypes.OrganizationId);

        return services;
    }
}
