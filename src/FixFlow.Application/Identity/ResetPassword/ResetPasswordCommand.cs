using System.Diagnostics.CodeAnalysis;
using System.Text;
using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Identity.ResetPassword;

/// <summary>A visitor sets a new password with the code from a reset link. It signs no one in.</summary>
/// <param name="Token">The code from the link's fragment (<see cref="PasswordResetCode"/>), as it is.</param>
public sealed record ResetPasswordCommand(string Token, string NewPassword) : IRequest<Result>
{
    // A logged command prints neither the code nor the password (S26)
    [SuppressMessage("Performance", "CA1822", Justification = "A record's PrintMembers is an instance method")]
    private bool PrintMembers(StringBuilder builder) => false;
}
