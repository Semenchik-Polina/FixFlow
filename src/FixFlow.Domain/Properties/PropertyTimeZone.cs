using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using NodaTime.TimeZones;

namespace FixFlow.Domain.Properties;

/// <summary>
/// The rules for a property's time zone, which its SLAs, due dates and appointments are computed in (§11): an IANA id
/// that names a place. Never a fixed offset, never a Windows id.
/// </summary>
public static class PropertyTimeZone
{
    /// <summary>The longest tzdb id has 32 characters; a test checks that every accepted one fits.</summary>
    public const int MaxLength = 64;

    /// <summary>
    /// Every tzdb id, canonical or alias, whose zone is a place in zone.tab or zone1970.tab, compared as written. That
    /// leaves out the fixed offsets (<c>UTC</c>, <c>Etc/GMT-2</c>), the POSIX-style zones (<c>EST5EDT</c>) and
    /// NodaTime's own offset ids (<c>UTC+02:00</c>), which <c>Tzdb.GetZoneOrNull</c> accepts. Aliases stay: some
    /// browsers still list old names such as <c>Europe/Minsk</c>. tzdb never removes an id, so a stored one stays valid.
    /// </summary>
    public static IReadOnlySet<string> Ids { get; } = PlaceIds();

    /// <summary>
    /// Every country's zones as zone.tab lists them, sorted: the ids that name its places (<c>NO</c> →
    /// <c>Europe/Oslo</c>), which the SPA offers for a property there. <see cref="IsValidFor"/> accepts more: aliases
    /// and the canonical zones those ids link to.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ByCountry { get; } = ZoneTabByCountry();

    // Each country's canonical zones: those of its zone.tab ids, and the zone1970.tab zones that name it
    private static readonly FrozenDictionary<string, FrozenSet<string>> _canonicalByCountry = CanonicalByCountry();

    /// <summary>Whether <paramref name="timeZoneId"/> can be a property's time zone, as written: it isn't trimmed.</summary>
    /// <param name="timeZoneId">The IANA id to check.</param>
    /// <param name="error">Why it can't, written for the user; null when it can.</param>
    public static bool IsValid(string timeZoneId, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(timeZoneId);

        error = Ids.Contains(timeZoneId)
            ? null
            : "Choose the time zone of the property's location, such as Europe/Warsaw. Fixed offsets and Windows time zones aren't accepted.";
        return error is null;
    }

    /// <summary>
    /// Whether <paramref name="timeZoneId"/> is a zone of <paramref name="countryCode"/>: its canonical zone is that of
    /// one of the country's zone.tab ids, or a zone1970.tab zone that names the country. So aliases (<c>Europe/Kiev</c>
    /// for UA) and the canonical zone a country's id links to (<c>Europe/Berlin</c> for NO) pass. Check
    /// <see cref="IsValid"/> and the country first: this explains only the mismatch.
    /// </summary>
    /// <param name="timeZoneId">The IANA id, as written.</param>
    /// <param name="countryCode">The property's ISO 3166 alpha-2 country code.</param>
    /// <param name="error">Why it isn't, written for the user; null when it is.</param>
    public static bool IsValidFor(string timeZoneId, string countryCode, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(timeZoneId);
        ArgumentNullException.ThrowIfNull(countryCode);

        var inCountry = TzdbDateTimeZoneSource.Default.CanonicalIdMap.TryGetValue(timeZoneId, out var canonical)
            && _canonicalByCountry.TryGetValue(countryCode, out var zones)
            && zones.Contains(canonical);
        error = inCountry ? null : "Choose a time zone in the property's country.";
        return error is null;
    }

    private static FrozenDictionary<string, IReadOnlyList<string>> ZoneTabByCountry() =>
        TzdbDateTimeZoneSource.Default.ZoneLocations!
            .GroupBy(location => location.CountryCode, StringComparer.Ordinal)
            .ToFrozenDictionary(
                country => country.Key,
                IReadOnlyList<string> (country) =>
                    [.. country.Select(location => location.ZoneId).Order(StringComparer.Ordinal)],
                StringComparer.Ordinal);

    private static FrozenDictionary<string, FrozenSet<string>> CanonicalByCountry()
    {
        var source = TzdbDateTimeZoneSource.Default;
        var zoneTab = source.ZoneLocations!.Select(location => (Country: location.CountryCode, Zone: location.ZoneId));
        var zone1970 = source.Zone1970Locations!.SelectMany(
            location => location.Countries.Select(country => (Country: country.Code, Zone: location.ZoneId)));

        return zoneTab.Concat(zone1970)
            .GroupBy(pair => pair.Country, StringComparer.Ordinal)
            .ToFrozenDictionary(
                country => country.Key,
                country => country.Select(pair => source.CanonicalIdMap[pair.Zone]).ToFrozenSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
    }

    private static FrozenSet<string> PlaceIds()
    {
        // The embedded tzdb data always has both location tables
        var source = TzdbDateTimeZoneSource.Default;
        var places = source.ZoneLocations!.Select(location => location.ZoneId)
            .Concat(source.Zone1970Locations!.Select(location => location.ZoneId))
            .ToHashSet(StringComparer.Ordinal);

        return source.CanonicalIdMap
            .Where(alias => places.Contains(alias.Value))
            .Select(alias => alias.Key)
            .ToFrozenSet(StringComparer.Ordinal);
    }
}
