using FixFlow.Domain.Properties;
using NodaTime;

namespace FixFlow.Domain.Tests.Properties;

public class PropertyTests
{
    private static readonly Address _address = Address.Create("Main Street 1", null, "Springfield", "00-001", "PL");
    private static readonly Instant _now = Instant.FromUtc(2026, 11, 3, 12, 0);

    [Fact]
    public void Create_ValidInput_TrimsNameAndKeepsAddressAndZone()
    {
        var property = Property.Create("  Riverside Apartments ", _address, "Europe/Warsaw");

        Assert.Equal("Riverside Apartments", property.Name);
        Assert.Same(_address, property.Address);
        Assert.Equal("Europe/Warsaw", property.TimeZoneId);
        Assert.Null(property.DeletedAt);
    }

    [Fact]
    public void Create_NameOfMaxLength_Succeeds()
    {
        var name = new string('n', Property.NameMaxLength);

        Assert.Equal(name, Property.Create(name, _address, "Europe/Warsaw").Name);
    }

    [Theory]
    [InlineData("", "A property's name is required.")]
    [InlineData("   ", "A property's name is required.")]
    [InlineData("Riverside\nApartments", "A property's name can't contain line breaks or other control characters.")]
    [InlineData("Riverside\0", "A property's name can't contain line breaks or other control characters.")]
    public void Create_InvalidName_Throws(string name, string message)
    {
        var exception = Assert.Throws<DomainException>(() => Property.Create(name, _address, "Europe/Warsaw"));
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void Create_NameTooLong_Throws()
    {
        var exception = Assert.Throws<DomainException>(
            () => Property.Create(new string('n', Property.NameMaxLength + 1), _address, "Europe/Warsaw"));
        Assert.Equal("A property's name can't be longer than 100 characters.", exception.Message);
    }

    [Theory]
    [InlineData("Central European Standard Time")]
    [InlineData("UTC+02:00")]
    public void Create_InvalidTimeZone_ThrowsWithTheZoneError(string timeZoneId)
    {
        PropertyTimeZone.IsValid(timeZoneId, out var error);

        var exception = Assert.Throws<DomainException>(() => Property.Create("Riverside", _address, timeZoneId));
        Assert.Equal(error, exception.Message);
    }

    [Fact]
    public void Create_NullArgument_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Property.Create(null!, _address, "Europe/Warsaw"));
        Assert.Throws<ArgumentNullException>(() => Property.Create("Riverside", null!, "Europe/Warsaw"));
        Assert.Throws<ArgumentNullException>(() => Property.Create("Riverside", _address, null!));
    }

    [Fact]
    public void Archive_ActiveProperty_SetsDeletedAt()
    {
        var property = Property.Create("Riverside", _address, "Europe/Warsaw");

        property.Archive(_now);

        Assert.Equal(_now, property.DeletedAt);
    }

    [Fact]
    public void Archive_ArchivedProperty_Throws()
    {
        var property = Property.Create("Riverside", _address, "Europe/Warsaw");
        property.Archive(_now);

        var exception = Assert.Throws<DomainException>(() => property.Archive(_now + Duration.FromHours(1)));
        Assert.Equal("This property is already archived.", exception.Message);
        Assert.Equal(_now, property.DeletedAt);
    }
}
