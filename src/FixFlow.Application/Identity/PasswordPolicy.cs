namespace FixFlow.Application.Identity;

/// <summary>
/// The password rule (S3): a minimum length, and no composition rules, as NIST SP 800-63B advises. Identity's
/// options and the validators of the commands that set a password (T1.3, T1.4) both read it from here.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 10;

    /// <summary>Room for a long passphrase, but a bound on what every sign-in hashes.</summary>
    public const int MaxLength = 128;
}
