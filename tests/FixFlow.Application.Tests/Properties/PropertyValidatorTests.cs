using FixFlow.Application.Properties;
using FixFlow.Application.Properties.CreateProperty;
using FixFlow.Application.Properties.UpdateProperty;
using FixFlow.Domain.Properties;

namespace FixFlow.Application.Tests.Properties;

/// <summary>
/// Creating and updating a property take the same fields and the same rules: each case runs through both validators,
/// which must fail on the same fields.
/// </summary>
public class PropertyValidatorTests
{
    private static readonly PropertyAddress _address = new("Main Street 1", null, "Springfield", null, "PL");

    [Fact]
    public void Validate_ValidInput_Passes()
    {
        Assert.Empty(Failed("Riverside", _address, "Europe/Warsaw"));
        // A blank optional part is no part
        Assert.Empty(Failed("Riverside", _address with { Line2 = "  ", PostalCode = "" }, "Europe/Warsaw"));
        // An alias, as some browsers still list it
        Assert.Empty(Failed("Riverside", _address, "Europe/Kiev"));
    }

    [Fact]
    public void Validate_PaddedTextWithinLimitsOnceTrimmed_Passes()
    {
        var address = new PropertyAddress(
            $" {new string('l', Address.LineMaxLength)} ",
            $" {new string('l', Address.LineMaxLength)} ",
            $" {new string('c', Address.CityMaxLength)} ",
            $" {new string('p', Address.PostalCodeMaxLength)} ",
            "PL");

        Assert.Empty(Failed($"  {new string('n', Property.NameMaxLength)}  ", address, "Europe/Warsaw"));
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("Riverside\nApartments")]
    public void Validate_InvalidName_Fails(string name)
    {
        Assert.Equal(["Name"], Failed(name, _address, "Europe/Warsaw"));
    }

    [Fact]
    public void Validate_NameTooLong_Fails()
    {
        Assert.Equal(["Name"], Failed(new string('n', Property.NameMaxLength + 1), _address, "Europe/Warsaw"));
    }

    [Fact]
    public void Validate_NoAddress_FailsOnAddress()
    {
        Assert.Equal(["Address"], Failed("Riverside", null, "Europe/Warsaw"));
    }

    public static TheoryData<PropertyAddress, string> InvalidAddresses => new()
    {
        { _address with { Line1 = " " }, "Address.Line1" },
        { _address with { Line1 = new string('l', Address.LineMaxLength + 1) }, "Address.Line1" },
        { _address with { Line1 = "Main\tStreet 1" }, "Address.Line1" },
        { _address with { Line2 = new string('l', Address.LineMaxLength + 1) }, "Address.Line2" },
        { _address with { Line2 = "Floor\n3" }, "Address.Line2" },
        { _address with { City = "" }, "Address.City" },
        { _address with { City = new string('c', Address.CityMaxLength + 1) }, "Address.City" },
        { _address with { PostalCode = new string('p', Address.PostalCodeMaxLength + 1) }, "Address.PostalCode" },
        { _address with { CountryCode = "" }, "Address.CountryCode" },
        { _address with { CountryCode = "pl" }, "Address.CountryCode" },
        { _address with { CountryCode = "POL" }, "Address.CountryCode" },
        { _address with { CountryCode = " PL" }, "Address.CountryCode" },
    };

    [Theory]
    [MemberData(nameof(InvalidAddresses))]
    public void Validate_InvalidAddressPart_FailsOnThatPart(PropertyAddress address, string field)
    {
        Assert.Equal([field], Failed("Riverside", address, "Europe/Warsaw"));
    }

    [Fact]
    public void Validate_UnknownCountry_FailsWithTheDomainsMessage()
    {
        Assert.False(Address.IsValidCountryCode("XX", out var expected));

        var failure = Assert.Single(new CreatePropertyValidator()
            .Validate(new CreatePropertyCommand("Riverside", _address with { CountryCode = "XX" }, "Europe/Warsaw"))
            .Errors);

        Assert.Equal("Address.CountryCode", failure.PropertyName);
        Assert.Equal(expected, failure.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Central European Standard Time")]
    [InlineData("UTC+02:00")]
    [InlineData("Etc/GMT-2")]
    [InlineData("europe/warsaw")]
    // Taken as written
    [InlineData(" Europe/Warsaw")]
    public void Validate_InvalidTimeZone_FailsOnTimeZone(string timeZone)
    {
        Assert.Equal(["TimeZone"], Failed("Riverside", _address, timeZone));
    }

    [Fact]
    public void Validate_FixedOffset_FailsWithTheDomainsMessage()
    {
        Assert.False(PropertyTimeZone.IsValid("UTC+02:00", out var expected));

        var failure = Assert.Single(new UpdatePropertyValidator()
            .Validate(new UpdatePropertyCommand(Guid.CreateVersion7(), "Riverside", _address, "UTC+02:00"))
            .Errors);

        Assert.Equal("TimeZone", failure.PropertyName);
        Assert.Equal(expected, failure.ErrorMessage);
    }

    [Fact]
    public void Validate_MissingFields_FailOncePerFieldWithoutThrowing()
    {
        Assert.Equal(["Name", "Address", "TimeZone"], Failed(null!, null, null!));
        Assert.Equal(
            ["Address.Line1", "Address.City", "Address.CountryCode"],
            Failed("Riverside", new PropertyAddress(null!, null, null!, null, null!), "Europe/Warsaw"));
    }

    /// <summary>The failed fields of both validators, which must agree.</summary>
    private static List<string> Failed(string name, PropertyAddress? address, string timeZone)
    {
        var created = new CreatePropertyValidator().Validate(new CreatePropertyCommand(name, address!, timeZone));
        var updated = new UpdatePropertyValidator().Validate(
            new UpdatePropertyCommand(Guid.CreateVersion7(), name, address!, timeZone));

        List<string> failed = [.. created.Errors.Select(e => e.PropertyName)];
        Assert.Equal(failed, updated.Errors.Select(e => e.PropertyName));
        return failed;
    }
}
