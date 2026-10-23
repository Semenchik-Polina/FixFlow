using FixFlow.Domain.Organizations;

namespace FixFlow.Domain.Tests.Organizations;

public class OrganizationTests
{
    [Fact]
    public void Create_ValidNameAndSlug_TrimsNameAndKeepsSlug()
    {
        var organization = Organization.Create("  Acme Property Management ", "acme");

        Assert.Equal("Acme Property Management", organization.Name);
        Assert.Equal("acme", organization.Slug);
    }

    [Fact]
    public void Create_NameOfMaxLengthOnceTrimmed_Succeeds()
    {
        var name = new string('n', Organization.NameMaxLength);

        Assert.Equal(name, Organization.Create($" {name} ", "acme").Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_Throws(string name)
    {
        var exception = Assert.Throws<DomainException>(() => Organization.Create(name, "acme"));
        Assert.Equal("An organization needs a name.", exception.Message);
    }

    [Fact]
    public void Create_NameTooLong_Throws()
    {
        var exception = Assert.Throws<DomainException>(
            () => Organization.Create(new string('n', Organization.NameMaxLength + 1), "acme"));
        Assert.Equal("An organization's name can't be longer than 100 characters.", exception.Message);
    }

    [Theory]
    [InlineData("Acme\0Homes")]
    [InlineData("Acme\0")] // Trim doesn't remove NUL
    [InlineData("Acme\nHomes")]
    [InlineData("Acme\tHomes")]
    public void Create_NameWithControlCharacter_Throws(string name)
    {
        var exception = Assert.Throws<DomainException>(() => Organization.Create(name, "acme"));
        Assert.Equal("An organization's name can't contain line breaks or other control characters.", exception.Message);
    }

    [Fact]
    public void Create_NameEndingInLineBreak_TrimsIt() => Assert.Equal("Acme", Organization.Create("Acme\n", "acme").Name);

    [Theory]
    [InlineData("ab")]
    [InlineData("Acme")]
    [InlineData("login")]
    public void Create_InvalidSlug_ThrowsWithSlugError(string slug)
    {
        OrganizationSlug.IsValid(slug, out var error);

        var exception = Assert.Throws<DomainException>(() => Organization.Create("Acme", slug));
        Assert.Equal(error, exception.Message);
    }

    [Fact]
    public void Create_NullArgument_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Organization.Create(null!, "acme"));
        Assert.Throws<ArgumentNullException>(() => Organization.Create("Acme", null!));
    }
}
