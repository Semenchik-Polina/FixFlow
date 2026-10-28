using FixFlow.Application.Identity;
using FixFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.API.Auth;

/// <summary><see cref="IAuthSession"/> with Identity's sign-in manager and its application cookie (plan P26).</summary>
internal sealed class SignInManagerAuthSession(SignInManager<User> signInManager, FixFlowDbContext db) : IAuthSession
{
    public Task<SignInOutcome> PasswordSignInAsync(
        User user, string password, bool isPersistent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(password);

        return UnderRowLockAsync(
            user,
            () => signInManager.PasswordSignInAsync(user, password, isPersistent, lockoutOnFailure: true),
            cancellationToken);
    }

    public Task<SignInOutcome> CheckPasswordAsync(User user, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(password);

        return UnderRowLockAsync(
            user,
            () => signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true),
            cancellationToken);
    }

    public Task SignInAsync(User user, bool isPersistent)
    {
        ArgumentNullException.ThrowIfNull(user);

        return signInManager.SignInAsync(user, isPersistent);
    }

    public Task RefreshSignInAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        // Keeps the request's cookie properties (persistence, expiry) and puts the user's current stamp in the cookie
        return signInManager.RefreshSignInAsync(user);
    }

    public Task SignOutAsync() => signInManager.SignOutAsync();

    /// <summary>A password check that counts towards the lockout, with the account's row locked.</summary>
    private async Task<SignInOutcome> UnderRowLockAsync(
        User user, Func<Task<SignInResult>> check, CancellationToken cancellationToken)
    {
        // Identity checks the lockout against the row it loaded, and doesn't count a failed attempt whose save loses a
        // concurrency race, so parallel attempts could each see a clean count (S3). Locking the account's row makes
        // them take turns, and the reload gives each one the count the one before it left.
        // A caller who leaves stops the wait for the lock and the reload: no check has run then, so nothing goes
        // uncounted, and the connection goes back to the pool instead of waiting for the attempt ahead.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM identity.users WHERE id = {user.Id} FOR UPDATE", cancellationToken);
        await db.Entry(user).ReloadAsync(cancellationToken);

        // Neither the check (UserManager takes no token) nor the commit stops: a check that ran counts
        var result = await check();
        await transaction.CommitAsync(CancellationToken.None);

        return result switch
        {
            { Succeeded: true } => SignInOutcome.Succeeded,
            { IsLockedOut: true } => SignInOutcome.LockedOut,
            // NotAllowed (an unconfirmed email, P3) and RequiresTwoFactor (MFA, Phase 2) can't happen in the MVP
            _ => SignInOutcome.Failed,
        };
    }
}
