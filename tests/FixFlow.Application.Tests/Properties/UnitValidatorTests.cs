using FixFlow.Application.Properties.CreateUnit;
using FixFlow.Application.Properties.ListProperties;
using FixFlow.Application.Properties.RenameUnit;
using FixFlow.Domain.Properties;

namespace FixFlow.Application.Tests.Properties;

/// <summary>A unit's name, which creating and renaming check the same way, and the property list's name filter.</summary>
public class UnitValidatorTests
{
    [Fact]
    public void Validate_PaddedNameWithinLimitOnceTrimmed_Passes()
    {
        Assert.Empty(FailedUnitName($" {new string('n', Unit.NameMaxLength)} "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("4\nB")]
    [InlineData("123456789012345678901234567890123456789012345678901")]
    public void Validate_InvalidUnitName_FailsOnName(string? name)
    {
        Assert.Equal(["Name"], FailedUnitName(name!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  river ")]
    public void Validate_NameFilter_BlankOrWithinLimit_Passes(string? name)
    {
        Assert.True(new ListPropertiesValidator().Validate(new ListPropertiesQuery(1, 25, name)).IsValid);
    }

    [Theory]
    [InlineData("river\0")]
    [InlineData("12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901")]
    public void Validate_InvalidNameFilter_FailsOnName(string name)
    {
        var result = new ListPropertiesValidator().Validate(new ListPropertiesQuery(1, 25, name));

        Assert.Equal(["Name"], result.Errors.Select(e => e.PropertyName));
    }

    /// <summary>The failed fields of both unit validators, which must agree.</summary>
    private static List<string> FailedUnitName(string name)
    {
        var created = new CreateUnitValidator().Validate(new CreateUnitCommand(Guid.CreateVersion7(), name));
        var renamed = new RenameUnitValidator().Validate(
            new RenameUnitCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), name));

        List<string> failed = [.. created.Errors.Select(e => e.PropertyName)];
        Assert.Equal(failed, renamed.Errors.Select(e => e.PropertyName));
        return failed;
    }
}
