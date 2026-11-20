using FluentValidation;
using static FixFlow.Application.Validation.TextValidationRules;

namespace FixFlow.Application.Properties.RenameUnit;

public sealed class RenameUnitValidator : AbstractValidator<RenameUnitCommand>
{
    public RenameUnitValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => Trimmed(command.Name)).UnitName().OverridePropertyName(nameof(RenameUnitCommand.Name));
    }
}
