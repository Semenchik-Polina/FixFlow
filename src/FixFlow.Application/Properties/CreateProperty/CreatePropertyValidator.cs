using FluentValidation;
using static FixFlow.Application.Validation.TextValidationRules;

namespace FixFlow.Application.Properties.CreateProperty;

public sealed class CreatePropertyValidator : AbstractValidator<CreatePropertyCommand>
{
    public CreatePropertyValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => Trimmed(command.Name))
            .PropertyName()
            .OverridePropertyName(nameof(CreatePropertyCommand.Name));
        RuleFor(command => command.Address).NotNull().SetValidator(new PropertyAddressValidator());
        RuleFor(command => command.TimeZone).TimeZone();
    }
}
