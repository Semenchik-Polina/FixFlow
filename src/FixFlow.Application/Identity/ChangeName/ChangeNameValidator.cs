using FluentValidation;
using static FixFlow.Application.Validation.TextValidationRules;

namespace FixFlow.Application.Identity.ChangeName;

public sealed class ChangeNameValidator : AbstractValidator<ChangeNameCommand>
{
    public ChangeNameValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => Trimmed(command.FirstName)).PersonName().OverridePropertyName(nameof(ChangeNameCommand.FirstName));
        RuleFor(command => Trimmed(command.LastName)).PersonName().OverridePropertyName(nameof(ChangeNameCommand.LastName));
    }
}
