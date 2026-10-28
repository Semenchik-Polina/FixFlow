using FixFlow.Application.Validation;
using FluentValidation;

namespace FixFlow.Application.Identity;

/// <summary>
/// The account rules every command shares, so signup (T1.3), reset and change password (T1.4) can't drift apart.
/// Each starts with <c>NotEmpty</c>; validators set <c>RuleLevelCascadeMode = CascadeMode.Stop</c>, so a rule reports
/// its first failure only.
/// </summary>
public static class IdentityValidationRules
{
    // RFC 5322's specials, but the dot, which separates atoms
    private const string Specials = "()<>[]:;@\\,\"";

    /// <summary>
    /// Identity's own email check (an <c>@</c> that is neither first nor last), within its column. Identity also stores
    /// the email's NFC form, uppercased, in a column of the same length, and NFC can be longer (U+0958 becomes two
    /// characters), so that form must fit too. No control characters either: Identity rejects a line break, which would
    /// otherwise pass here and fail as a 500. And a bare address (<see cref="IsBareAddress"/>): Identity's check passes
    /// <c>Name &lt;someone@example.com&gt;</c> and <c>someone@example.com(Name)</c>, which an SMTP library reads as an
    /// address with a display name or a comment, so the account's mail would go to an inbox other than the one stored.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> Email<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .NotEmpty()
            .MaximumLength(User.EmailMaxLength)
            .Must(email => email is null || email.Normalize().Length <= User.EmailMaxLength)
            .WithMessage("'{PropertyName}' is too long.")
            .EmailAddress()
            .Must(email => email is null || IsBareAddress(email))
            .WithMessage("'{PropertyName}' is not a valid email address.")
            .NoControlCharacters();

    /// <summary>A password that is being set (S3): its length is the only rule. Taken as written, never trimmed.</summary>
    public static IRuleBuilderOptions<T, string?> NewPassword<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().Length(PasswordPolicy.MinLength, PasswordPolicy.MaxLength);

    /// <summary>A first or last name.</summary>
    public static IRuleBuilderOptions<T, string?> PersonName<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().MaximumLength(User.NameMaxLength).NoControlCharacters();

    /// <summary>
    /// A local part, an <c>@</c> and a domain, each made of atoms apart by single dots (RFC 5322's dot-atom), so the
    /// SMTP sender sends it as it is. That leaves out a display name, a comment, a quoted local part, a domain literal,
    /// a second <c>@</c>, and a leading, trailing or double dot, which MimeKit rewrites or can't read. Non-ASCII
    /// characters are allowed (RFC 6532), and an IDN domain in either form.
    /// </summary>
    private static bool IsBareAddress(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at >= 0 && IsDotAtom(email[..at]) && IsDotAtom(email[(at + 1)..]);
    }

    private static bool IsDotAtom(string part) =>
        part.Split('.').All(atom =>
            atom.Length > 0 && !atom.Any(c => char.IsWhiteSpace(c) || Specials.Contains(c, StringComparison.Ordinal)));
}
