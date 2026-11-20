using System.Diagnostics.CodeAnalysis;
using System.Text;
using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Properties.UpdateProperty;

/// <summary>
/// A Manager or Admin replaces a property's name, address and time zone (from a <see cref="PropertyRequest"/>, with
/// the id from the route).
/// </summary>
public sealed record UpdatePropertyCommand(Guid Id, string Name, PropertyAddress Address, string TimeZone) : IRequest<Result>
{
    // A logged command prints no personal data (S26): a property is often named after its address
    [SuppressMessage("Performance", "CA1822", Justification = "A record's PrintMembers is an instance method")]
    private bool PrintMembers(StringBuilder builder) => false;
}
