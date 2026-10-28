namespace FixFlow.Application.Identity;

/// <summary>
/// The caller's browser session (plan P26). Signing in writes the auth cookie, which needs ASP.NET Core, so the API
/// implements this port with Identity's sign-in manager (P22). Handlers find and create users with
/// <c>UserManager</c> directly.
/// </summary>
public interface IAuthSession
{
    /// <summary>
    /// Checks the password and signs the user in when it's right. Every failure counts towards the lockout (S3), and a
    /// locked-out account isn't signed in even with the right password.
    /// </summary>
    /// <param name="isPersistent">
    /// A cookie that outlives the browser session ("Keep me signed in"); otherwise a session cookie.
    /// </param>
    Task<SignInOutcome> PasswordSignInAsync(User user, string password, bool isPersistent);

    /// <summary>
    /// Checks the password of a signed-in user before a sensitive change, and signs no one in. Like
    /// <see cref="PasswordSignInAsync"/>, every failure counts towards the lockout (S3), so a stolen session can't be
    /// used to guess the password, and a locked-out account fails even with the right password.
    /// </summary>
    Task<SignInOutcome> CheckPasswordAsync(User user, string password);

    /// <summary>Signs in a user whose credentials the caller has just set, such as a new account.</summary>
    Task SignInAsync(User user, bool isPersistent);

    /// <summary>
    /// Re-issues this browser's cookie after the user's security stamp changed, keeping whether it is persistent: this
    /// session goes on, and every other one ends on its next request (plan T1.4 D1).
    /// </summary>
    Task RefreshSignInAsync(User user);

    /// <summary>
    /// Removes the cookie from this browser. Cookie auth keeps no server-side session, so a copy of the cookie stays
    /// valid until it expires or the security stamp changes.
    /// </summary>
    Task SignOutAsync();
}
