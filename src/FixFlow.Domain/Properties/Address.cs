using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using NodaTime.TimeZones;

namespace FixFlow.Domain.Properties;

/// <summary>
/// A property's postal address, compared by value. It is personal data (S24): a rented house's address is its
/// tenant's home. So it never prints its parts, and logs never carry it.
/// </summary>
public sealed record Address
{
    public const int LineMaxLength = 200;

    public const int CityMaxLength = 100;

    public const int PostalCodeMaxLength = 20;

    public const int CountryCodeLength = 2;

    // The ISO 3166 alpha-2 codes of every country in tzdb's zone.tab, which lists every inhabited one. NodaTime is the
    // one country list Domain can use; .NET's RegionInfo depends on the host's ICU data.
    private static readonly FrozenSet<string> _countryCodes = TzdbDateTimeZoneSource.Default.ZoneLocations!
        .Select(location => location.CountryCode)
        .ToFrozenSet(StringComparer.Ordinal);

    private Address(string line1, string? line2, string city, string? postalCode, string countryCode)
    {
        Line1 = line1;
        Line2 = line2;
        City = city;
        PostalCode = postalCode;
        CountryCode = countryCode;
    }

    /// <summary>The street and number.</summary>
    public string Line1 { get; }

    /// <summary>An apartment, floor or building name; null when there is none.</summary>
    public string? Line2 { get; }

    public string City { get; }

    /// <summary>Null where the country has no postal codes.</summary>
    public string? PostalCode { get; }

    /// <summary>ISO 3166 alpha-2, uppercase: <c>PL</c>.</summary>
    public string CountryCode { get; }

    /// <summary>Each part is trimmed; a blank optional part becomes null.</summary>
    /// <param name="countryCode">Taken as written: two uppercase letters of a known country.</param>
    /// <exception cref="DomainException">A part is missing, too long or contains a control character, or the country
    /// code isn't known.</exception>
    public static Address Create(string line1, string? line2, string city, string? postalCode, string countryCode)
    {
        var validLine1 = DomainText.Required(line1, LineMaxLength, "The street address");
        var validLine2 = DomainText.Optional(line2, LineMaxLength, "The second address line");
        var validCity = DomainText.Required(city, CityMaxLength, "The city");
        var validPostalCode = DomainText.Optional(postalCode, PostalCodeMaxLength, "The postal code");

        if (!IsValidCountryCode(countryCode, out var countryCodeError))
        {
            throw new DomainException(countryCodeError);
        }

        return new Address(validLine1, validLine2, validCity, validPostalCode, countryCode);
    }

    /// <summary>Whether <paramref name="countryCode"/> is a known country's code, as written: it isn't trimmed or uppercased.</summary>
    /// <param name="countryCode">The ISO 3166 alpha-2 code to check.</param>
    /// <param name="error">Why it isn't, written for the user; null when it is.</param>
    public static bool IsValidCountryCode(string countryCode, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(countryCode);

        error = _countryCodes.Contains(countryCode)
            ? null
            : "The country must be a two-letter ISO 3166 code in capitals, such as PL.";
        return error is null;
    }

    [SuppressMessage("Performance", "CA1822", Justification = "A record's PrintMembers is an instance method")]
    private bool PrintMembers(StringBuilder builder) => false;
}
