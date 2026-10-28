using FluentValidation;

namespace FixFlow.Application.Identity.ResetPassword;

/// <summary>
/// The code's presence and a bound on its length only: a code that is malformed, expired or someone else's is the
/// handler's one 422, which says nothing about why.
/// </summary>
public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    /// <summary>Well above a real code (a few hundred characters), and a bound on what a request makes the app decode.</summary>
    public const int TokenMaxLength = 2048;

    public ResetPasswordValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Token).NotEmpty().MaximumLength(TokenMaxLength);
        RuleFor(command => command.NewPassword).NewPassword();
    }
}
