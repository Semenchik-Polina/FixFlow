using FixFlow.Application.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace FixFlow.Application.Identity.LogIn;

/// <summary>
/// Signs a user in with their email and password (plan P26). A wrong password, an unknown email and a locked-out
/// account get one answer, so it never tells whether an account exists.
/// </summary>
internal sealed partial class LogInHandler(UserManager<User> users, IAuthSession session, ILogger<LogInHandler> logger)
    : IRequestHandler<LogInCommand, Result>
{
    /// <summary>Not a field error: it can't say which field is wrong without telling whether the email has an account.</summary>
    private const string FailureDetail =
        "The email or password is incorrect, or the account is locked for a while after too many failed attempts.";

    /// <summary>Hashes the password of an unknown email; the default hasher doesn't read the user.</summary>
    private static readonly User _unknownUser = new("unknown@example.invalid", "Unknown", "User");

    public async Task<Result> Handle(LogInCommand command, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(command.Email.Trim());
        if (user is null)
        {
            // Costs about what checking a wrong password does, so the time taken says little about the email
            _ = users.PasswordHasher.HashPassword(_unknownUser, command.Password);
            LogUnknownEmail();
            return Result.Unprocessable(FailureDetail);
        }

        switch (await session.PasswordSignInAsync(user, command.Password, command.RememberMe))
        {
            case SignInOutcome.Succeeded:
                LogSignedIn(user.Id);
                return Result.Success();
            case SignInOutcome.LockedOut:
                LogLockedOut(user.Id);
                return Result.Unprocessable(FailureDetail);
            default:
                LogWrongPassword(user.Id);
                return Result.Unprocessable(FailureDetail);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} signed in")]
    private partial void LogSignedIn(Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sign-in failed: unknown email")]
    private partial void LogUnknownEmail();

    [LoggerMessage(Level = LogLevel.Information, Message = "Sign-in failed for user {UserId}: wrong password")]
    private partial void LogWrongPassword(Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sign-in failed for user {UserId}: locked out")]
    private partial void LogLockedOut(Guid userId);
}
