using FixFlow.Application.Validation;
using FixFlow.Domain.Properties;
using FluentValidation;

namespace FixFlow.Application.Properties;

/// <summary>
/// <see cref="Property"/>'s and <see cref="Unit"/>'s rules as validation rules, so bad input is a 400 with a field error
/// rather than the domain's 422. The domain still enforces them. Each starts with <c>NotEmpty</c>; validators set
/// <c>RuleLevelCascadeMode = CascadeMode.Stop</c>, so a rule reports its first failure only.
/// </summary>
public static class PropertyValidationRules
{
    public static IRuleBuilderOptions<T, string?> PropertyName<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().MaximumLength(Property.NameMaxLength).NoControlCharacters();

    public static IRuleBuilderOptions<T, string?> UnitName<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().MaximumLength(Unit.NameMaxLength).NoControlCharacters();

    /// <summary>
    /// <see cref="PropertyTimeZone.IsValid"/>, then <see cref="PropertyTimeZone.IsValidFor"/> the property's country,
    /// each with its message. Taken as written: never trimmed.
    /// </summary>
    /// <param name="rule">The time zone's rule.</param>
    /// <param name="countryCode">The property's country code; the zone isn't checked against a missing or invalid
    /// one, which is the address's error.</param>
    public static IRuleBuilderOptionsConditions<T, string?> TimeZone<T>(
        this IRuleBuilder<T, string?> rule, Func<T, string?> countryCode) =>
        rule.NotEmpty().Custom((timeZone, context) =>
        {
            // NotEmpty reports a null
            if (timeZone is null)
            {
                return;
            }

            if (!PropertyTimeZone.IsValid(timeZone, out var error))
            {
                context.AddFailure(error);
                return;
            }

            var country = countryCode(context.InstanceToValidate);
            if (country is not null
                && Address.IsValidCountryCode(country, out _)
                && !PropertyTimeZone.IsValidFor(timeZone, country, out var countryError))
            {
                context.AddFailure(countryError);
            }
        });

    /// <summary><see cref="Address.IsValidCountryCode"/>, with its message. Taken as written: never uppercased.</summary>
    public static IRuleBuilderOptionsConditions<T, string?> CountryCode<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().Custom((countryCode, context) =>
        {
            // NotEmpty reports a null
            if (countryCode is not null && !Address.IsValidCountryCode(countryCode, out var error))
            {
                context.AddFailure(error);
            }
        });
}
