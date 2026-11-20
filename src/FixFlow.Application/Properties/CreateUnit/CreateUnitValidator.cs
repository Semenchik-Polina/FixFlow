using FluentValidation;
using static FixFlow.Application.Validation.TextValidationRules;

namespace FixFlow.Application.Properties.CreateUnit;

public sealed class CreateUnitValidator : AbstractValidator<CreateUnitCommand>
{
    public CreateUnitValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => Trimmed(command.Name)).UnitName().OverridePropertyName(nameof(CreateUnitCommand.Name));
    }
}
