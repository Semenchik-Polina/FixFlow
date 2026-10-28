using FixFlow.Application.Identity;
using FixFlow.Application.Identity.ForgotPassword;
using FluentValidation.Results;

namespace FixFlow.Application.Tests.Identity;

public class ForgotPasswordValidatorTests
{
    private readonly ForgotPasswordValidator _validator = new();

    [Theory]
    [InlineData("user@example.test")]
    [InlineData("  USER@example.test ")]
    public void Validate_Email_Passes(string email)
    {
        Assert.True(_validator.Validate(new ForgotPasswordCommand(email)).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData(" @example.test")]
    [InlineData("us\ner@example.test")]
    [InlineData("Mallory <victim@example.test>")]
    public void Validate_MissingOrMalformedEmail_Fails(string? email)
    {
        var result = _validator.Validate(new ForgotPasswordCommand(email!));

        Assert.Equal([nameof(ForgotPasswordCommand.Email)], Failed(result));
    }

    [Fact]
    public void Validate_EmailLongerThanItsColumn_Fails()
    {
        var email = new string('a', User.EmailMaxLength - "@example.test".Length + 1) + "@example.test";

        var result = _validator.Validate(new ForgotPasswordCommand(email));

        Assert.Equal([nameof(ForgotPasswordCommand.Email)], Failed(result));
    }

    private static List<string> Failed(ValidationResult result) => [.. result.Errors.Select(e => e.PropertyName)];
}
