using FluentValidation;
using static FixFlow.Application.Validation.TextValidationRules;

namespace FixFlow.Application.Identity.LogIn;

/// <summary>
/// Only presence and the maximum lengths: an email or password that breaks the other rules simply matches no account,
/// and answers like a wrong password.
/// </summary>
public sealed class LogInValidator : AbstractValidator<LogInCommand>
{
    public LogInValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => Trimmed(command.Email))
            .NotEmpty()
            .MaximumLength(User.EmailMaxLength)
            .OverridePropertyName(nameof(LogInCommand.Email));
        RuleFor(command => command.Password).NotEmpty().MaximumLength(PasswordPolicy.MaxLength);
    }
}
