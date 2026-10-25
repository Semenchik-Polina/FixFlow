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

    /// <summary>Signs in a user whose credentials the caller has just set, such as a new account.</summary>
    Task SignInAsync(User user, bool isPersistent);

    /// <summary>
    /// Removes the cookie from this browser. Cookie auth keeps no server-side session, so a copy of the cookie stays
    /// valid until it expires or the security stamp changes (T1.4).
    /// </summary>
    Task SignOutAsync();
}
