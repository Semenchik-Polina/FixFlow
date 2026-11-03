using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Identity.LogOut;

/// <summary>
/// Removes the cookie from this browser only: a copy of it stays valid until it expires or the security stamp changes
/// (T1.4).
/// </summary>
internal sealed class LogOutHandler(IAuthSession session) : IRequestHandler<LogOutCommand, Result>
{
    public async Task<Result> Handle(LogOutCommand command, CancellationToken cancellationToken)
    {
        await session.SignOutAsync();
        return Result.Success();
    }
}
