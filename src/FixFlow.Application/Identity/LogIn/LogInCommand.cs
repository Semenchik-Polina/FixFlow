using System.Text;
using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Identity.LogIn;

/// <param name="RememberMe">
/// "Keep me signed in": a cookie that outlives the browser session (Identity's 14 days, sliding); otherwise a session
/// cookie.
/// </param>
public sealed record LogInCommand(string Email, string Password, bool RememberMe) : IRequest<Result>
{
    // A logged command prints neither the password nor the email (S26)
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("RememberMe = ").Append(RememberMe);
        return true;
    }
}
