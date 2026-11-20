using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace FixFlow.Application.Properties;

/// <summary>
/// The body of creating or updating a property: one shape for both, so the SPA's dialog has one type. The endpoint
/// turns it into the command, with the id from the route. Prints nothing: a property is often named after its street
/// address, which is personal data (S24).
/// </summary>
public sealed record PropertyRequest(string Name, PropertyAddress Address, string TimeZone)
{
    [SuppressMessage("Performance", "CA1822", Justification = "A record's PrintMembers is an instance method")]
    private bool PrintMembers(StringBuilder builder) => false;
}

/// <summary>The body of creating or renaming a unit. The endpoint turns it into the command, with the ids from the route.</summary>
public sealed record UnitRequest(string Name);
