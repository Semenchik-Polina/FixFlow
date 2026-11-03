using FixFlow.Application.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace FixFlow.API.Auth;

/// <summary>
/// Checks the cookie's security stamp on every request (plan T1.4 D1) and leaves the cookie alone when it matches.
/// Identity's validator rebuilds the principal and re-issues the cookie after each check. With a check on every
/// request, a slow response to a request that started before a password change would then put back a cookie with the
/// old stamp, and sign the user out on their next request. The claims are ids only, so there is nothing to rebuild.
/// Sliding expiration still renews the cookie on its own schedule.
/// </summary>
internal sealed class NonRenewingSecurityStampValidator(
    IOptions<SecurityStampValidatorOptions> options,
    SignInManager<User> signInManager,
    ILoggerFactory logger) : SecurityStampValidator<User>(options, signInManager, logger)
{
    protected override Task SecurityStampVerified(User user, CookieValidatePrincipalContext context) =>
        Task.CompletedTask;
}
