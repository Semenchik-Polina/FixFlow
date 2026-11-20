using FluentValidation;
using static FixFlow.Application.Validation.TextValidationRules;

namespace FixFlow.Application.Properties.UpdateProperty;

public sealed class UpdatePropertyValidator : AbstractValidator<UpdatePropertyCommand>
{
    public UpdatePropertyValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => Trimmed(command.Name))
            .PropertyName()
            .OverridePropertyName(nameof(UpdatePropertyCommand.Name));
        RuleFor(command => command.Address).NotNull().SetValidator(new PropertyAddressValidator());
        RuleFor(command => command.TimeZone).TimeZone();
    }
}
