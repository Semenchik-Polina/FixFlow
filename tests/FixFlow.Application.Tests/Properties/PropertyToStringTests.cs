using FixFlow.Application.Properties;
using FixFlow.Application.Properties.CreateProperty;
using FixFlow.Application.Properties.UpdateProperty;

namespace FixFlow.Application.Tests.Properties;

/// <summary>
/// A request or command that reaches a log prints no address, nor the property's name, which is often its street
/// address (S24, S26).
/// </summary>
public class PropertyToStringTests
{
    private static readonly PropertyAddress _address = new("Main Street 1", "Floor 3", "Springfield", "00-001", "PL");

    [Fact]
    public void PropertyAddress_ToString_LeavesOutEveryPart()
    {
        Assert.Equal("PropertyAddress { }", _address.ToString());
    }

    [Fact]
    public void PropertyRequest_ToString_LeavesOutNameAndAddress()
    {
        Assert.Equal("PropertyRequest { }", new PropertyRequest("Main Street 1", _address, "Europe/Warsaw").ToString());
    }

    [Fact]
    public void CreatePropertyCommand_ToString_LeavesOutNameAndAddress()
    {
        Assert.Equal(
            "CreatePropertyCommand { }", new CreatePropertyCommand("Main Street 1", _address, "Europe/Warsaw").ToString());
    }

    [Fact]
    public void UpdatePropertyCommand_ToString_LeavesOutNameAndAddress()
    {
        var command = new UpdatePropertyCommand(Guid.CreateVersion7(), "Main Street 1", _address, "Europe/Warsaw");

        Assert.Equal("UpdatePropertyCommand { }", command.ToString());
    }
}
