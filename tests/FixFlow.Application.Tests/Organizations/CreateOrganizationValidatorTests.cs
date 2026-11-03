using FixFlow.Application.Organizations.CreateOrganization;
using FixFlow.Domain.Organizations;
using FluentValidation.Results;

namespace FixFlow.Application.Tests.Organizations;

public class CreateOrganizationValidatorTests
{
    private readonly CreateOrganizationValidator _validator = new();

    [Fact]
    public void Validate_PaddedNameWithinLimitOnceTrimmed_Passes()
    {
        var result = _validator.Validate(new CreateOrganizationCommand(
            $"  {new string('a', Organization.NameMaxLength)}  ", "test-org"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("Acme\r\nRentals")]
    public void Validate_InvalidName_Fails(string name)
    {
        var result = _validator.Validate(new CreateOrganizationCommand(name, "test-org"));

        Assert.Equal([nameof(CreateOrganizationCommand.Name)], Failed(result));
    }

    [Fact]
    public void Validate_NameTooLong_Fails()
    {
        var result = _validator.Validate(new CreateOrganizationCommand(
            new string('a', Organization.NameMaxLength + 1), "test-org"));

        Assert.Equal([nameof(CreateOrganizationCommand.Name)], Failed(result));
    }

    [Fact]
    public void Validate_ReservedSlug_FailsWithTheSlugRulesMessage()
    {
        Assert.False(OrganizationSlug.IsValid("api", out var expected));

        var failure = Assert.Single(_validator.Validate(new CreateOrganizationCommand("Test Org", "api")).Errors);

        Assert.Equal(nameof(CreateOrganizationCommand.Slug), failure.PropertyName);
        Assert.Equal(expected, failure.ErrorMessage);
    }

    [Fact]
    public void Validate_MissingFields_FailOncePerFieldWithoutThrowing()
    {
        var result = _validator.Validate(new CreateOrganizationCommand(null!, null!));

        Assert.Equal([nameof(CreateOrganizationCommand.Name), nameof(CreateOrganizationCommand.Slug)], Failed(result));
    }

    private static List<string> Failed(ValidationResult result) => [.. result.Errors.Select(e => e.PropertyName)];
}
