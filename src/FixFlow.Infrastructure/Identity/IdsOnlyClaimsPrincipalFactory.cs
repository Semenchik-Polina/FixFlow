using System.Security.Claims;
using FixFlow.Application.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace FixFlow.Infrastructure.Identity;

/// <summary>
/// The signed-in principal carries ids, not personal data (S26). Identity's factory also adds the email as a claim:
/// nothing needs it from the cookie, and anything that logs the principal would leak it. The username is already the
/// id (plan P22). A handler that needs the caller's email reads it from the database.
/// </summary>
internal sealed class IdsOnlyClaimsPrincipalFactory(UserManager<User> userManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<User>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        foreach (var email in identity.FindAll(Options.ClaimsIdentity.EmailClaimType).ToList())
        {
            identity.RemoveClaim(email);
        }

        return identity;
    }
}
