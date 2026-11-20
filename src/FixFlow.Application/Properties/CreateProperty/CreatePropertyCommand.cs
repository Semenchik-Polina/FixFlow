using System.Diagnostics.CodeAnalysis;
using System.Text;
using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Properties.CreateProperty;

/// <summary>A Manager or Admin adds a property to the organization (from a <see cref="PropertyRequest"/>).</summary>
public sealed record CreatePropertyCommand(string Name, PropertyAddress Address, string TimeZone)
    : IRequest<Result<PropertyResponse>>
{
    // A logged command prints no personal data (S26): a property is often named after its address
    [SuppressMessage("Performance", "CA1822", Justification = "A record's PrintMembers is an instance method")]
    private bool PrintMembers(StringBuilder builder) => false;
}
