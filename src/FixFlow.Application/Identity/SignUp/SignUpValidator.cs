using FixFlow.Application.Organizations;
using FluentValidation;
using static FixFlow.Application.Validation.TextValidationRules;

namespace FixFlow.Application.Identity.SignUp;

public sealed class SignUpValidator : AbstractValidator<SignUpCommand>
{
    public SignUpValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => Trimmed(command.Email)).Email().OverridePropertyName(nameof(SignUpCommand.Email));
        RuleFor(command => command.Password).NewPassword();
        RuleFor(command => Trimmed(command.FirstName)).PersonName().OverridePropertyName(nameof(SignUpCommand.FirstName));
        RuleFor(command => Trimmed(command.LastName)).PersonName().OverridePropertyName(nameof(SignUpCommand.LastName));
        RuleFor(command => Trimmed(command.OrganizationName))
            .OrganizationName()
            .OverridePropertyName(nameof(SignUpCommand.OrganizationName));
        RuleFor(command => command.Slug).Slug();
    }
}
