namespace FixFlow.Application.Identity;

/// <summary>The outcome of <see cref="IAuthSession.PasswordSignInAsync"/>.</summary>
public enum SignInOutcome
{
    Succeeded,

    /// <summary>The password is wrong.</summary>
    Failed,

    /// <summary>Too many failed attempts (S3): no sign-in until the lockout ends, whatever the password.</summary>
    LockedOut,
}
