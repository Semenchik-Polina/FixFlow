using FixFlow.Application.Auth;
using FixFlow.Application.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace FixFlow.Application.Identity.ChangePassword;

/// <summary>
/// Changes the caller's password once the current one checks out. The check counts towards the lockout (S3), so a
/// stolen session can't be used to guess the password. The new password changes the security stamp: this browser gets
/// a cookie with the new stamp, and every other session ends on its next request (plan T1.4 D1).
/// </summary>
internal sealed partial class ChangePasswordHandler(
    UserManager<User> users,
    IAuthSession session,
    ICurrentUser currentUser,
    ILogger<ChangePasswordHandler> logger) : IRequestHandler<ChangePasswordCommand, Result>
{
    /// <summary>One message for a wrong password and a lockout, like login's.</summary>
    private const string WrongPasswordMessage =
        "The current password is incorrect, or the account is locked for a while after too many failed attempts.";

    public async Task<Result> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Changing the password needs a signed-in user.");

        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.NotFound();
        }

        var outcome = await session.CheckPasswordAsync(user, command.CurrentPassword, cancellationToken);
        if (outcome != SignInOutcome.Succeeded)
        {
            if (outcome == SignInOutcome.LockedOut)
            {
                LogLockedOut(user.Id);
            }
            else
            {
                LogWrongPassword(user.Id);
            }

            return Result.FieldError(nameof(ChangePasswordCommand.CurrentPassword), WrongPasswordMessage);
        }

        // Not ChangePasswordAsync, which would hash the current password again (a second PBKDF2 run). A reset token
        // made here costs microseconds, and ResetPasswordAsync does the rest the same way: it validates the new
        // password, changes the stamp and saves once. A change that saved since the check makes that save lose on the
        // concurrency stamp: a 409.
        var token = await users.GeneratePasswordResetTokenAsync(user);
        (await users.ResetPasswordAsync(user, token, command.NewPassword)).ThrowIfFailed("Changing the password");

        await session.RefreshSignInAsync(user);
        LogChanged(user.Id);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} changed their password")]
    private partial void LogChanged(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password change failed for user {UserId}: wrong password")]
    private partial void LogWrongPassword(Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Password change failed for user {UserId}: locked out")]
    private partial void LogLockedOut(Guid userId);
}
