using FixFlow.Application.Identity;
using Microsoft.AspNetCore.Identity;

namespace FixFlow.API.Auth;

/// <summary><see cref="IAuthSession"/> with Identity's sign-in manager and its application cookie (plan P26).</summary>
internal sealed class SignInManagerAuthSession(SignInManager<User> signInManager) : IAuthSession
{
    public async Task<SignInOutcome> PasswordSignInAsync(User user, string password, bool isPersistent)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(password);

        var result = await signInManager.PasswordSignInAsync(user, password, isPersistent, lockoutOnFailure: true);

        return result switch
        {
            { Succeeded: true } => SignInOutcome.Succeeded,
            { IsLockedOut: true } => SignInOutcome.LockedOut,
            // NotAllowed (an unconfirmed email, P3) and RequiresTwoFactor (MFA, Phase 2) can't happen in the MVP
            _ => SignInOutcome.Failed,
        };
    }

    public Task SignInAsync(User user, bool isPersistent)
    {
        ArgumentNullException.ThrowIfNull(user);

        return signInManager.SignInAsync(user, isPersistent);
    }

    public Task SignOutAsync() => signInManager.SignOutAsync();
}
