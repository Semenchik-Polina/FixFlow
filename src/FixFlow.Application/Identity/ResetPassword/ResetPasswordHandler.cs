using FixFlow.Application.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace FixFlow.Application.Identity.ResetPassword;

/// <summary>
/// Sets a new password with a reset code (S4). Identity checks the token and changes the security stamp, which ends
/// every session of the account (plan T1.4 D1) and voids this token and every other outstanding one. A malformed code,
/// an unknown user, and an expired, used or foreign token all get one answer. A save to the account that came first
/// (a failed login, a reset with the same token) is a 409: the token was valid, so the link works again unless that
/// save was a reset.
/// </summary>
internal sealed partial class ResetPasswordHandler(UserManager<User> users, ILogger<ResetPasswordHandler> logger)
    : IRequestHandler<ResetPasswordCommand, Result>
{
    /// <summary>Not a field error: the SPA's only remedy is a new link.</summary>
    private const string InvalidLinkDetail = "This reset link is invalid or has expired. Ask for a new one.";

    public async Task<Result> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        if (!ResetPasswordCode.TryParse(command.Token, out var userId, out var token))
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

        // A valid token proves the owner controls the inbox, so a reset also ends a lockout. Identity saves these with
        // the new password, in its one save.
        var (lockoutEnd, accessFailedCount) = (user.LockoutEnd, user.AccessFailedCount);
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;

        // Identity checks the token first, and changes nothing else when it fails
        var result = await users.ResetPasswordAsync(user, token, command.NewPassword);
        if (result.HasError(nameof(IdentityErrorDescriber.InvalidToken)))
        {
            // Put back, so a forged token leaves the tracked user as it was, even if something in the request saves
            // later
            (user.LockoutEnd, user.AccessFailedCount) = (lockoutEnd, accessFailedCount);
            LogInvalidToken(user.Id);
            return Result.Unprocessable(InvalidLinkDetail);
        }

        // A lost race on the concurrency stamp is a 409. The validator mirrors Identity's password rules, so anything
        // else is a bug.
        result.ThrowIfFailed("Resetting the password");
        LogReset(user.Id);
        return Result.Success();
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
