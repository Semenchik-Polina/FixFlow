using FixFlow.Application.Email;
using FixFlow.Application.Errors;
using FixFlow.Application.Identity.ResetPassword;
using FixFlow.Application.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace FixFlow.Application.Identity.ForgotPassword;

/// <summary>
/// Emails a reset link to an account's address (S4). It succeeds whether or not the email has an account, and
/// whether or not the email could be sent: a failure that only a known email can have would tell that it has one. The
/// link is built from configuration, never from the request (<see cref="ISpaLinks"/>).
/// </summary>
internal sealed partial class ForgotPasswordHandler(
    UserManager<User> users,
    ISpaLinks links,
    IEmailSender email,
    IExceptionRedactor redactor,
    ILogger<ForgotPasswordHandler> logger) : IRequestHandler<ForgotPasswordCommand, Result>
{
    public async Task<Result> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(command.Email.Trim());
        if (user is null)
        {
            LogUnknownEmail();
            return Result.Success();
        }

        try
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var link = links.ResetPassword(PasswordResetCode.Format(user.Id, token));
            // The stored address, not the one typed: they can differ in case
            await email.SendAsync(PasswordResetEmail.Create(user.Email!, user.FirstName, link), cancellationToken);
        }
        // The caller's cancellation stays a cancellation. Anything else, a wrong setting included, is logged without its
        // personal data: only known emails get this far, so a 500 here would tell that they have an account.
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogEmailFailed(redactor.ForLog(exception), user.Id);
            return Result.Success();
        }

        LogEmailSent(user.Id);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset requested for an unknown email")]
    private partial void LogUnknownEmail();

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset email sent to user {UserId}")]
    private partial void LogEmailSent(Guid userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Password reset email to user {UserId} could not be sent")]
    private partial void LogEmailFailed(Exception exception, Guid userId);
}
