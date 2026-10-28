using FluentValidation;

namespace FixFlow.Application.Identity.ChangePassword;

/// <summary>
/// The current password is only checked for presence and the maximum length, like login's: a wrong one is the
/// handler's field error.
/// </summary>
public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.CurrentPassword).NotEmpty().MaximumLength(PasswordPolicy.MaxLength);
        RuleFor(command => command.NewPassword).NewPassword();
    }
}
