using FixFlow.Application.Identity;
using FixFlow.Application.Identity.ResetPassword;
using FluentValidation.Results;

namespace FixFlow.Application.Tests.Identity;

public class ResetPasswordValidatorTests
{
    private const string Code = "user-id.reset_code";

    private readonly ResetPasswordValidator _validator = new();

    [Fact]
    public void Validate_AnyNonEmptyCode_Passes()
    {
        // Whether the code is well-formed is the handler's one 422, not a 400 that would say why
        Assert.True(_validator.Validate(new ResetPasswordCommand("x", "correct horse battery")).IsValid);
    }

    [Theory]
    [InlineData(PasswordPolicy.MinLength - 1, false)]
    [InlineData(PasswordPolicy.MinLength, true)]
    [InlineData(PasswordPolicy.MaxLength, true)]
    [InlineData(PasswordPolicy.MaxLength + 1, false)]
    public void Validate_NewPasswordLength_AllowsMinToMax(int length, bool valid)
    {
        var result = _validator.Validate(new ResetPasswordCommand(Code, new string('a', length)));

        Assert.Equal(valid ? [] : [nameof(ResetPasswordCommand.NewPassword)], Failed(result));
    }

    [Theory]
    [InlineData(ResetPasswordValidator.TokenMaxLength, true)]
    [InlineData(ResetPasswordValidator.TokenMaxLength + 1, false)]
    public void Validate_CodeLength_IsBounded(int length, bool valid)
    {
        var result = _validator.Validate(new ResetPasswordCommand(new string('a', length), "correct horse battery"));

        Assert.Equal(valid ? [] : [nameof(ResetPasswordCommand.Token)], Failed(result));
    }

    [Fact]
    public void Validate_MissingFields_FailOncePerFieldWithoutThrowing()
    {
        var result = _validator.Validate(new ResetPasswordCommand(null!, null!));

        Assert.Equal([nameof(ResetPasswordCommand.Token), nameof(ResetPasswordCommand.NewPassword)], Failed(result));
    }

    private static List<string> Failed(ValidationResult result) => [.. result.Errors.Select(e => e.PropertyName)];
}
