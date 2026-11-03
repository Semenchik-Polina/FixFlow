using FluentValidation;
using static FixFlow.Application.Validation.TextValidationRules;

namespace FixFlow.Application.Identity.ForgotPassword;

/// <summary>A malformed email is a 400: it can't have an account, so the answer tells nothing.</summary>
public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => Trimmed(command.Email)).Email().OverridePropertyName(nameof(ForgotPasswordCommand.Email));
    }
}
