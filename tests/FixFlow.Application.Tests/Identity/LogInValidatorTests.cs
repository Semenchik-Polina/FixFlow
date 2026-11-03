using FixFlow.Application.Identity;
using FixFlow.Application.Identity.LogIn;
using FluentValidation.Results;

namespace FixFlow.Application.Tests.Identity;

public class LogInValidatorTests
{
    private readonly LogInValidator _validator = new();

    [Fact]
    public void Validate_ShortPassword_Passes()
    {
        // Login checks presence and the maximum lengths only: a short password just matches no account
        var result = _validator.Validate(new LogInCommand("user@example.test", "a", RememberMe: false));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData(null, null)]
    public void Validate_EmptyFields_Fail(string? email, string? password)
    {
        var result = _validator.Validate(new LogInCommand(email!, password!, RememberMe: false));

        Assert.Equal([nameof(LogInCommand.Email), nameof(LogInCommand.Password)], Failed(result));
    }

    [Fact]
    public void Validate_TooLongFields_Fail()
    {
        var email = new string('a', User.EmailMaxLength - "@example.test".Length + 1) + "@example.test";
        var password = new string('a', PasswordPolicy.MaxLength + 1);

        var result = _validator.Validate(new LogInCommand(email, password, RememberMe: true));

        Assert.Equal([nameof(LogInCommand.Email), nameof(LogInCommand.Password)], Failed(result));
    }

    private static List<string> Failed(ValidationResult result) => [.. result.Errors.Select(e => e.PropertyName)];
}
