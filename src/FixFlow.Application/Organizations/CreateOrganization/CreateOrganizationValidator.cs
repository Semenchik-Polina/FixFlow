using FluentValidation;
using static FixFlow.Application.Validation.TextValidationRules;

namespace FixFlow.Application.Organizations.CreateOrganization;

public sealed class CreateOrganizationValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => Trimmed(command.Name))
            .OrganizationName()
            .OverridePropertyName(nameof(CreateOrganizationCommand.Name));
        RuleFor(command => command.Slug).Slug();
    }
}
