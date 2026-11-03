using FixFlow.Domain.Properties;
using NodaTime;

namespace FixFlow.Domain.Tests.Properties;

public class UnitTests
{
    private static readonly Guid _propertyId = Guid.CreateVersion7();
    private static readonly Instant _now = Instant.FromUtc(2026, 11, 3, 12, 0);

    [Fact]
    public void Create_ValidInput_TrimsNameAndKeepsProperty()
    {
        var unit = Unit.Create(_propertyId, " 4B ");

        Assert.Equal(_propertyId, unit.PropertyId);
        Assert.Equal("4B", unit.Name);
        Assert.Null(unit.DeletedAt);
    }

    [Fact]
    public void Create_NameOfMaxLength_Succeeds()
    {
        var name = new string('n', Unit.NameMaxLength);

        Assert.Equal(name, Unit.Create(_propertyId, name).Name);
    }

    [Theory]
    [InlineData("", "A unit's name is required.")]
    [InlineData("   ", "A unit's name is required.")]
    [InlineData("4\nB", "A unit's name can't contain line breaks or other control characters.")]
    public void Create_InvalidName_Throws(string name, string message)
    {
        var exception = Assert.Throws<DomainException>(() => Unit.Create(_propertyId, name));
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void Create_NameTooLong_Throws()
    {
        var exception = Assert.Throws<DomainException>(
            () => Unit.Create(_propertyId, new string('n', Unit.NameMaxLength + 1)));
        Assert.Equal("A unit's name can't be longer than 50 characters.", exception.Message);
    }

    [Fact]
    public void Create_InvalidArgument_Throws()
    {
        Assert.Throws<ArgumentException>(() => Unit.Create(Guid.Empty, "4B"));
        Assert.Throws<ArgumentNullException>(() => Unit.Create(_propertyId, null!));
    }

    [Fact]
    public void Archive_ActiveUnit_SetsDeletedAt()
    {
        var unit = Unit.Create(_propertyId, "4B");

        unit.Archive(_now);

        Assert.Equal(_now, unit.DeletedAt);
    }

    [Fact]
    public void Archive_ArchivedUnit_Throws()
    {
        var unit = Unit.Create(_propertyId, "4B");
        unit.Archive(_now);

        var exception = Assert.Throws<DomainException>(() => unit.Archive(_now + Duration.FromHours(1)));
        Assert.Equal("This unit is already archived.", exception.Message);
        Assert.Equal(_now, unit.DeletedAt);
    }
}
