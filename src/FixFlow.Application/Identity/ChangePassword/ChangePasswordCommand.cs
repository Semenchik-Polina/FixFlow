using System.Diagnostics.CodeAnalysis;
using System.Text;
using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Identity.ChangePassword;

/// <summary>
/// A signed-in user replaces their password, proving it with the current one. This browser stays signed in; every
/// other session ends.
/// </summary>
public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<Result>
{
    // A logged command prints neither password (S26)
    [SuppressMessage("Performance", "CA1822", Justification = "A record's PrintMembers is an instance method")]
    private bool PrintMembers(StringBuilder builder) => false;
}
