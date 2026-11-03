using FixFlow.Application.Identity.ChangeName;
using FixFlow.Application.Identity.ChangePassword;
using FixFlow.Application.Identity.ForgotPassword;
using FixFlow.Application.Identity.LogIn;
using FixFlow.Application.Identity.ResetPassword;
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

    [Fact]
    public void ForgotPasswordCommand_ToString_LeavesOutEmail()
    {
        Assert.Equal("ForgotPasswordCommand { }", new ForgotPasswordCommand(Email).ToString());
    }

    [Fact]
    public void ResetPasswordCommand_ToString_LeavesOutCodeAndPassword()
    {
        var text = new ResetPasswordCommand("user-id.reset_code", Password).ToString();

        Assert.Equal("ResetPasswordCommand { }", text);
    }

    [Fact]
    public void ChangePasswordCommand_ToString_LeavesOutBothPasswords()
    {
        var text = new ChangePasswordCommand(Password, "a brand new passphrase").ToString();

        Assert.Equal("ChangePasswordCommand { }", text);
    }

    [Fact]
    public void ChangeNameCommand_ToString_LeavesOutNames()
    {
        Assert.Equal("ChangeNameCommand { }", new ChangeNameCommand("Firstname", "Lastname").ToString());
    }
}
