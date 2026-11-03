using FixFlow.Domain.Properties;

namespace FixFlow.Domain.Tests.Properties;

public class AddressTests
{
    [Fact]
    public void Create_ValidParts_TrimsThem()
    {
        var address = Address.Create(" Main Street 1 ", " Flat 2 ", " Springfield ", " 00-001 ", "PL");

        Assert.Equal("Main Street 1", address.Line1);
        Assert.Equal("Flat 2", address.Line2);
        Assert.Equal("Springfield", address.City);
        Assert.Equal("00-001", address.PostalCode);
        Assert.Equal("PL", address.CountryCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankOptionalParts_AreNull(string? blank)
    {
        var address = Address.Create("Main Street 1", blank, "Springfield", blank, "PL");

        Assert.Null(address.Line2);
        Assert.Null(address.PostalCode);
    }

    [Theory]
    [InlineData("", "Springfield", "The street address is required.")]
    [InlineData("Main Street 1", "  ", "The city is required.")]
    public void Create_BlankRequiredPart_Throws(string line1, string city, string message)
    {
        var exception = Assert.Throws<DomainException>(() => Address.Create(line1, null, city, null, "PL"));
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void Create_PartsOfMaxLength_Succeed()
    {
        var address = Address.Create(
            new string('a', Address.LineMaxLength),
            new string('b', Address.LineMaxLength),
            new string('c', Address.CityMaxLength),
            new string('1', Address.PostalCodeMaxLength),
            "PL");

        Assert.Equal(Address.LineMaxLength, address.Line1.Length);
    }

    [Fact]
    public void Create_PartTooLong_Throws()
    {
        string TooLong(int maxLength) => new('x', maxLength + 1);

        Assert.Equal(
            "The street address can't be longer than 200 characters.",
            Assert.Throws<DomainException>(
                () => Address.Create(TooLong(Address.LineMaxLength), null, "Springfield", null, "PL")).Message);
        Assert.Equal(
            "The second address line can't be longer than 200 characters.",
            Assert.Throws<DomainException>(
                () => Address.Create("Main Street 1", TooLong(Address.LineMaxLength), "Springfield", null, "PL")).Message);
        Assert.Equal(
            "The city can't be longer than 100 characters.",
            Assert.Throws<DomainException>(
                () => Address.Create("Main Street 1", null, TooLong(Address.CityMaxLength), null, "PL")).Message);
        Assert.Equal(
            "The postal code can't be longer than 20 characters.",
            Assert.Throws<DomainException>(
                () => Address.Create("Main Street 1", null, "Springfield", TooLong(Address.PostalCodeMaxLength), "PL")).Message);
    }

    [Theory]
    [InlineData("Main\nStreet 1", "Springfield")]
    [InlineData("Main Street 1", "Spring\0field")]
    public void Create_PartWithControlCharacter_Throws(string line1, string city)
    {
        var exception = Assert.Throws<DomainException>(() => Address.Create(line1, null, city, null, "PL"));
        Assert.EndsWith("can't contain line breaks or other control characters.", exception.Message);
    }

    [Theory]
    [InlineData("pl")]
    [InlineData("POL")]
    [InlineData("P")]
    [InlineData(" PL")]
    [InlineData("XX")]
    [InlineData("")]
    public void Create_UnknownCountryCode_Throws(string countryCode)
    {
        var exception = Assert.Throws<DomainException>(
            () => Address.Create("Main Street 1", null, "Springfield", null, countryCode));
        Assert.Equal("The country must be a two-letter ISO 3166 code in capitals, such as PL.", exception.Message);
    }

    [Theory]
    [InlineData("PL")]
    [InlineData("UA")]
    [InlineData("US")]
    public void IsValidCountryCode_KnownCode_IsValid(string countryCode)
    {
        Assert.True(Address.IsValidCountryCode(countryCode, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("pl")]
    [InlineData("XX")]
    public void IsValidCountryCode_UnknownCode_ExplainsTheRule(string countryCode)
    {
        Assert.False(Address.IsValidCountryCode(countryCode, out var error));
        Assert.Equal("The country must be a two-letter ISO 3166 code in capitals, such as PL.", error);
    }

    [Fact]
    public void IsValidCountryCode_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Address.IsValidCountryCode(null!, out _));

    [Fact]
    public void Create_NullRequiredPart_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Address.Create(null!, null, "Springfield", null, "PL"));
        Assert.Throws<ArgumentNullException>(() => Address.Create("Main Street 1", null, null!, null, "PL"));
        Assert.Throws<ArgumentNullException>(() => Address.Create("Main Street 1", null, "Springfield", null, null!));
    }

    [Fact]
    public void ToString_LeavesOutTheAddress()
    {
        var address = Address.Create("Main Street 1", "Flat 2", "Springfield", "00-001", "PL");

        Assert.Equal("Address { }", address.ToString());
    }
}
