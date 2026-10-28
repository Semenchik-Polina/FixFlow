using FixFlow.Application.Auth;
using FixFlow.Application.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace FixFlow.Application.Identity.ChangeName;

/// <summary>
/// Changes the caller's own name. Saved through <c>UserManager</c>, whose store turns a lost race on the user's
/// concurrency stamp into a result this handler makes a 409 (plan P11); a save of the context would let EF Core's
/// exception through. The security stamp stays, so every session goes on.
/// </summary>
internal sealed partial class ChangeNameHandler(
    UserManager<User> users,
    ICurrentUser currentUser,
    ILogger<ChangeNameHandler> logger) : IRequestHandler<ChangeNameCommand, Result>
{
    public async Task<Result> Handle(ChangeNameCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Changing the name needs a signed-in user.");

        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.NotFound();
        }

        user.ChangeName(command.FirstName, command.LastName);
        (await users.UpdateAsync(user)).ThrowIfFailed("Changing the name");

        LogChanged(user.Id);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} changed their name")]
    private partial void LogChanged(Guid userId);
}
