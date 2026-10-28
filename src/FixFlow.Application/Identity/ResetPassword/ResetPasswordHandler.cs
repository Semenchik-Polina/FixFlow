using FixFlow.Application.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace FixFlow.Application.Identity.ResetPassword;

/// <summary>
/// Sets a new password with a reset code (S4). Identity checks the token and changes the security stamp, which ends
/// every session of the account (plan T1.4 D1) and voids this token and every other outstanding one. A malformed code,
/// an unknown user, an expired, used or foreign token, and a lost race with a reset of the same token all get one
/// answer.
/// </summary>
internal sealed partial class ResetPasswordHandler(UserManager<User> users, ILogger<ResetPasswordHandler> logger)
    : IRequestHandler<ResetPasswordCommand, Result>
{
    /// <summary>Not a field error: the SPA's only remedy is a new link.</summary>
    private const string InvalidLinkDetail = "This reset link is invalid or has expired. Ask for a new one.";

    public async Task<Result> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        if (!PasswordResetCode.TryParse(command.Token, out var userId, out var token))
        {
            LogMalformedCode();
            return Result.Unprocessable(InvalidLinkDetail);
        }

        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            LogMalformedCode();
            return Result.Unprocessable(InvalidLinkDetail);
        }

        // Checked before the lockout fields change, so an invalid token leaves the tracked user as it was, even if
        // something in the request saves later. ResetPasswordAsync checks it again.
        if (!await users.VerifyUserTokenAsync(
                user, users.Options.Tokens.PasswordResetTokenProvider, UserManager<User>.ResetPasswordTokenPurpose, token))
        {
            LogInvalidToken(user.Id);
            return Result.Unprocessable(InvalidLinkDetail);
        }

        // The owner proved they control the inbox, so a reset also ends a lockout. Identity saves these with the new
        // password, in its one save.
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;

        var result = await users.ResetPasswordAsync(user, token, command.NewPassword);
        if (result.Succeeded)
        {
            LogReset(user.Id);
            return Result.Success();
        }

        // ConcurrencyFailure: another save to the account came first, usually a reset with the same token. Any other
        // write in those milliseconds (a failed login) gets the same answer, and a new link works.
        if (result.HasError(nameof(IdentityErrorDescriber.InvalidToken))
            || result.HasError(nameof(IdentityErrorDescriber.ConcurrencyFailure)))
        {
            LogInvalidToken(user.Id);
            return Result.Unprocessable(InvalidLinkDetail);
        }

        // The validator mirrors Identity's password rules, so anything else is a bug
        throw result.Unexpected("Resetting the password");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset for user {UserId}")]
    private partial void LogReset(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset failed: malformed code or unknown user")]
    private partial void LogMalformedCode();

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Password reset failed for user {UserId}: invalid, expired or used token")]
    private partial void LogInvalidToken(Guid userId);
}
