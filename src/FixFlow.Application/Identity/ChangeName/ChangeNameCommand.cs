using System.Diagnostics.CodeAnalysis;
using System.Text;
using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Identity.ChangeName;

/// <summary>A signed-in user changes their own first and last name. Both are trimmed.</summary>
public sealed record ChangeNameCommand(string FirstName, string LastName) : IRequest<Result>
{
    // A logged command prints no personal data (S26)
    [SuppressMessage("Performance", "CA1822", Justification = "A record's PrintMembers is an instance method")]
    private bool PrintMembers(StringBuilder builder) => false;
}
