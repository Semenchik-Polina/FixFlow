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
    /// <summary>
    /// Identity's own email check (an <c>@</c> that is neither first nor last), within its column. No control
    /// characters either: Identity rejects a line break, which would otherwise pass here and fail as a 500.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> Email<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().MaximumLength(User.EmailMaxLength).EmailAddress().NoControlCharacters();

    /// <summary>A password that is being set (S3): its length is the only rule. Taken as written, never trimmed.</summary>
    public static IRuleBuilderOptions<T, string?> NewPassword<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().Length(PasswordPolicy.MinLength, PasswordPolicy.MaxLength);

    /// <summary>A first or last name.</summary>
    public static IRuleBuilderOptions<T, string?> PersonName<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().MaximumLength(User.NameMaxLength).NoControlCharacters();
}
