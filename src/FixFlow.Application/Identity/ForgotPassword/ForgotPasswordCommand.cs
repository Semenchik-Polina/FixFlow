using System.Diagnostics.CodeAnalysis;
using System.Text;
using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Identity.ForgotPassword;

/// <summary>
/// A visitor who can't sign in asks for a reset link by email. The answer is the same whether or not the email has an
/// account.
/// </summary>
public sealed record ForgotPasswordCommand(string Email) : IRequest<Result>
{
    // A logged command prints no email (S26)
    [SuppressMessage("Performance", "CA1822", Justification = "A record's PrintMembers is an instance method")]
    private bool PrintMembers(StringBuilder builder) => false;
}
