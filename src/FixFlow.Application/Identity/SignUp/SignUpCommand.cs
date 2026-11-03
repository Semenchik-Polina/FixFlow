using System.Text;
using FixFlow.Application.Organizations;
using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Identity.SignUp;

/// <summary>
/// A visitor creates their account and their first organization, of which they become the Admin, and is signed in.
/// </summary>
public sealed record SignUpCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string OrganizationName,
    string Slug) : IRequest<Result<OrganizationCreated>>
{
    // A logged command prints neither the password nor personal data (S26)
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Slug = ").Append(Slug);
        return true;
    }
}
