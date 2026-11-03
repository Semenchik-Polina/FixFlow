using FluentValidation;

namespace FixFlow.Application.Validation;

/// <summary>Rules for free text that the shared rules of each module build on.</summary>
public static class TextValidationRules
{
    /// <summary>
    /// A validator checks the trimmed value, which is what the handler stores, so a padded value can't pass the rules
    /// and then fail in the domain or in Identity (<c>" @x"</c> passes the email check, <c>"@x"</c> doesn't).
    /// FluentValidation 12 has no <c>Transform</c>, so a rule names its property itself:
    /// <c>RuleFor(c => Trimmed(c.Email)).Email().OverridePropertyName(nameof(c.Email))</c>.
    /// </summary>
    public static string? Trimmed(string? value) => value?.Trim();

    /// <summary>
    /// No line breaks or other control characters: PostgreSQL can't store NUL, and names go into email subjects.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> NoControlCharacters<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithMessage("'{PropertyName}' can't contain line breaks or other control characters.");
}
