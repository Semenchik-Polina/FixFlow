using FixFlow.Domain.Properties;
using NodaTime.TimeZones;

namespace FixFlow.Domain.Tests.Properties;

public class PropertyTimeZoneTests
{
    private const string Rule =
        "Choose the time zone of the property's location, such as Europe/Warsaw. Fixed offsets and Windows time zones aren't accepted.";

    private const string CountryRule = "Choose a time zone in the property's country.";

    [Theory]
    [InlineData("Europe/Warsaw")]
    [InlineData("Europe/Kyiv")]
    [InlineData("America/New_York")]
    [InlineData("Europe/Kiev")] // An alias of Europe/Kyiv, which some browsers still list
    [InlineData("Asia/Calcutta")] // An alias of Asia/Kolkata
    public void IsValid_ZoneOfAPlace_IsValid(string timeZoneId)
    {
        Assert.True(PropertyTimeZone.IsValid(timeZoneId, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("Central European Standard Time")] // Windows
    [InlineData("UTC+02:00")] // NodaTime's own fixed-offset id, which Tzdb.GetZoneOrNull accepts
    [InlineData("+02:00")]
    [InlineData("Etc/GMT-2")]
    [InlineData("UTC")]
    [InlineData("Etc/UTC")]
    [InlineData("EST5EDT")]
    [InlineData("europe/warsaw")]
    [InlineData(" Europe/Warsaw")]
    [InlineData("Europe/Atlantis")]
    [InlineData("")]
    public void IsValid_NotTheZoneOfAPlace_ExplainsTheRule(string timeZoneId)
    {
        Assert.False(PropertyTimeZone.IsValid(timeZoneId, out var error));
        Assert.Equal(Rule, error);
    }

    [Fact]
    public void IsValid_EveryZoneTabZone_IsValid()
    {
        var zoneTab = TzdbDateTimeZoneSource.Default.ZoneLocations!.Select(location => location.ZoneId);

        Assert.DoesNotContain(zoneTab, id => !PropertyTimeZone.IsValid(id, out _));
    }

    [Fact]
    public void Ids_LeaveOutEveryEtcZone() =>
        Assert.DoesNotContain(PropertyTimeZone.Ids, id => id.StartsWith("Etc/", StringComparison.Ordinal));

    [Fact]
    public void Ids_FitMaxLength() =>
        Assert.DoesNotContain(PropertyTimeZone.Ids, id => id.Length > PropertyTimeZone.MaxLength);

    [Fact]
    public void IsValid_Null_Throws() => Assert.Throws<ArgumentNullException>(() => PropertyTimeZone.IsValid(null!, out _));

    [Theory]
    [InlineData("Europe/Warsaw", "PL")]
    [InlineData("America/New_York", "US")]
    [InlineData("Europe/Oslo", "NO")] // zone.tab's id for Norway
    [InlineData("Europe/Berlin", "NO")] // Europe/Oslo's canonical zone: the same clock
    [InlineData("Europe/Kiev", "UA")] // An alias of Europe/Kyiv
    [InlineData("Asia/Calcutta", "IN")] // An alias of Asia/Kolkata
    public void IsValidFor_ZoneOfTheCountry_IsValid(string timeZoneId, string countryCode)
    {
        Assert.True(PropertyTimeZone.IsValidFor(timeZoneId, countryCode, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("America/New_York", "PL")]
    [InlineData("Europe/Warsaw", "US")]
    [InlineData("Europe/Kyiv", "PL")]
    [InlineData("Europe/Warsaw", "XX")]
    [InlineData("Europe/Atlantis", "PL")]
    [InlineData("Etc/GMT-1", "PL")]
    public void IsValidFor_ZoneOfAnotherCountry_ExplainsTheRule(string timeZoneId, string countryCode)
    {
        Assert.False(PropertyTimeZone.IsValidFor(timeZoneId, countryCode, out var error));
        Assert.Equal(CountryRule, error);
    }

    [Fact]
    public void ByCountry_EveryZoneTabCountry_ListsItsZoneTabZonesSorted()
    {
        var zoneTab = TzdbDateTimeZoneSource.Default.ZoneLocations!;

        Assert.Equal(
            zoneTab.Select(location => location.CountryCode).Distinct().Order(StringComparer.Ordinal),
            PropertyTimeZone.ByCountry.Keys.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(
            PropertyTimeZone.ByCountry,
            country => !country.Value.SequenceEqual(zoneTab
                .Where(location => location.CountryCode == country.Key)
                .Select(location => location.ZoneId)
                .Order(StringComparer.Ordinal)));
        Assert.Equal(["Europe/Warsaw"], PropertyTimeZone.ByCountry["PL"]);
        Assert.Equal(["Europe/Oslo"], PropertyTimeZone.ByCountry["NO"]);
    }

    [Fact]
    public void ByCountry_EveryListedZone_IsValidForItsCountry() =>
        Assert.DoesNotContain(
            PropertyTimeZone.ByCountry.SelectMany(
                country => country.Value.Select(zone => (Country: country.Key, Zone: zone))),
            listed => !PropertyTimeZone.IsValid(listed.Zone, out _)
                || !PropertyTimeZone.IsValidFor(listed.Zone, listed.Country, out _));

    [Fact]
    public void IsValidFor_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PropertyTimeZone.IsValidFor(null!, "PL", out _));
        Assert.Throws<ArgumentNullException>(() => PropertyTimeZone.IsValidFor("Europe/Warsaw", null!, out _));
    }
}
