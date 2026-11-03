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
