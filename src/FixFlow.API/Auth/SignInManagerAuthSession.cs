using FixFlow.Application.Identity;
using FixFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.API.Auth;

/// <summary><see cref="IAuthSession"/> with Identity's sign-in manager and its application cookie (plan P26).</summary>
internal sealed class SignInManagerAuthSession(SignInManager<User> signInManager, FixFlowDbContext db) : IAuthSession
{
    public async Task<SignInOutcome> PasswordSignInAsync(User user, string password, bool isPersistent)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(password);

        // Identity checks the lockout against the row it loaded, and doesn't count a failed attempt whose save loses a
        // concurrency race, so parallel attempts could each see a clean count (S3). Locking the account's row makes
        // them take turns, and the reload gives each one the count the one before it left.
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM identity.users WHERE id = {user.Id} FOR UPDATE");
        await db.Entry(user).ReloadAsync();

        var result = await signInManager.PasswordSignInAsync(user, password, isPersistent, lockoutOnFailure: true);
        await transaction.CommitAsync();

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
