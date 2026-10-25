using FixFlow.Application.Identity.LogIn;
using FixFlow.Application.Identity.SignUp;

namespace FixFlow.Application.Tests.Identity;

/// <summary>A command that reaches a log prints neither its password nor personal data (S26).</summary>
public class CommandToStringTests
{
    private const string Email = "user@example.test";
    private const string Password = "correct horse battery";

    [Fact]
    public void SignUpCommand_ToString_LeavesOutPasswordAndPersonalData()
    {
        var text = new SignUpCommand(Email, Password, "Firstname", "Lastname", "Test Org", "test-org").ToString();

        Assert.Equal("SignUpCommand { Slug = test-org }", text);
    }

    [Fact]
    public void LogInCommand_ToString_LeavesOutPasswordAndEmail()
    {
        var text = new LogInCommand(Email, Password, RememberMe: true).ToString();

        Assert.Equal("LogInCommand { RememberMe = True }", text);
    }
}
