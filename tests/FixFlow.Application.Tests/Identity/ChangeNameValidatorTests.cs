using FixFlow.Application.Identity;
using FixFlow.Application.Identity.ChangeName;
using FluentValidation.Results;

namespace FixFlow.Application.Tests.Identity;

public class ChangeNameValidatorTests
{
    private readonly ChangeNameValidator _validator = new();

    [Fact]
    public void Validate_PaddedNamesWithinLimitsOnceTrimmed_Pass()
    {
        var name = $" {new string('a', User.NameMaxLength)} ";

        Assert.True(_validator.Validate(new ChangeNameCommand(name, name)).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_BlankNames_Fail(string? name)
    {
        var result = _validator.Validate(new ChangeNameCommand(name!, name!));

        Assert.Equal([nameof(ChangeNameCommand.FirstName), nameof(ChangeNameCommand.LastName)], Failed(result));
    }

    [Fact]
    public void Validate_NameTooLong_Fails()
    {
        var result = _validator.Validate(new ChangeNameCommand("Ada", new string('a', User.NameMaxLength + 1)));

        Assert.Equal([nameof(ChangeNameCommand.LastName)], Failed(result));
    }

    [Theory]
    [InlineData("Ada\nBob")]
    [InlineData("Ada\0")]
    // Inside the name: trimming removes one at either end
    [InlineData("Ada\tLove")]
    public void Validate_ControlCharacters_Fail(string name)
    {
        var result = _validator.Validate(new ChangeNameCommand(name, "Lovelace"));

        Assert.Equal([nameof(ChangeNameCommand.FirstName)], Failed(result));
    }

    private static List<string> Failed(ValidationResult result) => [.. result.Errors.Select(e => e.PropertyName)];
}
