using FixFlow.Domain.Properties;
using NodaTime.TimeZones;

namespace FixFlow.Domain.Tests.Properties;

public class PropertyTimeZoneTests
{
    private const string Rule =
        "Choose the time zone of the property's location, such as Europe/Warsaw. Fixed offsets and Windows time zones aren't accepted.";

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
}
