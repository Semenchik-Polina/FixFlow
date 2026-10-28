using FixFlow.Application.Identity;
using FixFlow.Application.Identity.ChangePassword;
using FluentValidation.Results;

namespace FixFlow.Application.Tests.Identity;

public class ChangePasswordValidatorTests
{
    private const string Current = "correct horse battery";

    private readonly ChangePasswordValidator _validator = new();

    [Fact]
    public void Validate_ShortCurrentPassword_Passes()
    {
        // Like login: a current password that breaks the rules is simply wrong, which the handler answers
        Assert.True(_validator.Validate(new ChangePasswordCommand("a", "a brand new passphrase")).IsValid);
    }

    [Theory]
    [InlineData(PasswordPolicy.MinLength - 1, false)]
    [InlineData(PasswordPolicy.MinLength, true)]
    [InlineData(PasswordPolicy.MaxLength, true)]
    [InlineData(PasswordPolicy.MaxLength + 1, false)]
    public void Validate_NewPasswordLength_AllowsMinToMax(int length, bool valid)
    {
        var result = _validator.Validate(new ChangePasswordCommand(Current, new string('a', length)));

        Assert.Equal(valid ? [] : [nameof(ChangePasswordCommand.NewPassword)], Failed(result));
    }

    [Fact]
    public void Validate_CurrentPasswordTooLong_Fails()
    {
        var result = _validator.Validate(
            new ChangePasswordCommand(new string('a', PasswordPolicy.MaxLength + 1), "a brand new passphrase"));

        Assert.Equal([nameof(ChangePasswordCommand.CurrentPassword)], Failed(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_EmptyFields_Fail(string? password)
    {
        var result = _validator.Validate(new ChangePasswordCommand(password!, password!));

        Assert.Equal(
            [nameof(ChangePasswordCommand.CurrentPassword), nameof(ChangePasswordCommand.NewPassword)], Failed(result));
    }

    private static List<string> Failed(ValidationResult result) => [.. result.Errors.Select(e => e.PropertyName)];
}
