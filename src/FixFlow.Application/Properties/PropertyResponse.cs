using System.Linq.Expressions;
using FixFlow.Domain.Properties;

namespace FixFlow.Application.Properties;

/// <summary>A property as every property endpoint returns it.</summary>
/// <param name="TimeZone">Its IANA time zone (<see cref="Property.TimeZoneId"/>), which the SPA formats its dates in.</param>
public sealed record PropertyResponse(Guid Id, string Name, PropertyAddress Address, string TimeZone)
{
    /// <summary>The one mapping: queries project with it, and <see cref="From"/> runs it on a loaded property.</summary>
    internal static readonly Expression<Func<Property, PropertyResponse>> Projection = property => new PropertyResponse(
        property.Id,
        property.Name,
        new PropertyAddress(
            property.Address.Line1,
            property.Address.Line2,
            property.Address.City,
            property.Address.PostalCode,
            property.Address.CountryCode),
        property.TimeZoneId);

    private static readonly Func<Property, PropertyResponse> _from = Projection.Compile();

    internal static PropertyResponse From(Property property) => _from(property);
}
