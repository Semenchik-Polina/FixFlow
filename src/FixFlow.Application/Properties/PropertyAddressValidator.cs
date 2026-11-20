using FixFlow.Application.Validation;
using FixFlow.Domain.Properties;
using FluentValidation;
using static FixFlow.Application.Validation.TextValidationRules;

namespace FixFlow.Application.Properties;

/// <summary>
/// <see cref="Address.Create"/>'s rules, for a request's address: <c>RuleFor(c => c.Address).NotNull().SetValidator(...)</c>
/// gives field errors such as <c>address.line1</c>.
/// </summary>
public sealed class PropertyAddressValidator : AbstractValidator<PropertyAddress>
{
    public PropertyAddressValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(address => Trimmed(address.Line1))
            .NotEmpty().MaximumLength(Address.LineMaxLength).NoControlCharacters()
            .OverridePropertyName(nameof(PropertyAddress.Line1));
        RuleFor(address => Trimmed(address.Line2))
            .MaximumLength(Address.LineMaxLength).NoControlCharacters()
            .OverridePropertyName(nameof(PropertyAddress.Line2));
        RuleFor(address => Trimmed(address.City))
            .NotEmpty().MaximumLength(Address.CityMaxLength).NoControlCharacters()
            .OverridePropertyName(nameof(PropertyAddress.City));
        RuleFor(address => Trimmed(address.PostalCode))
            .MaximumLength(Address.PostalCodeMaxLength).NoControlCharacters()
            .OverridePropertyName(nameof(PropertyAddress.PostalCode));
        RuleFor(address => address.CountryCode).CountryCode();
    }
}
