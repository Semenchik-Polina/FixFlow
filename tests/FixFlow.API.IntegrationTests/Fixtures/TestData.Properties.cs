namespace FixFlow.API.IntegrationTests.Fixtures;

public static partial class TestData
{
    /// <summary>A property with a unique name and a made-up address (D23). Vary it with <c>with</c>.</summary>
    public static PropertyRequest Property() =>
        new(Name("Property"), new PropertyAddress("1 Test Street", null, "Testville", "00-001", "PL"), "Europe/Warsaw");

    /// <summary>A unique unit name, well within <c>Unit.NameMaxLength</c>.</summary>
    public static string UnitName() => $"U-{Token()}";
}

/// <summary>The body of <c>POST /properties</c> and <c>PUT /properties/{id}</c>, as the SPA sends it.</summary>
public sealed record PropertyRequest(string Name, PropertyAddress? Address, string TimeZone);

/// <summary>A property's address, in requests and responses.</summary>
public sealed record PropertyAddress(string Line1, string? Line2, string City, string? PostalCode, string CountryCode);

/// <summary>The body of creating or renaming a unit.</summary>
public sealed record UnitRequest(string Name);

/// <summary>A property as the property endpoints return it.</summary>
public sealed record PropertyResponse(Guid Id, string Name, PropertyAddress Address, string TimeZone);

/// <summary>A unit as the unit endpoints return it.</summary>
public sealed record UnitResponse(Guid Id, Guid PropertyId, string Name);

/// <summary>One page of a list endpoint.</summary>
public sealed record ListPage<T>(T[] Items, int Page, int PageSize, int TotalCount);
