using FixFlow.Application.Identity;
using FixFlow.Application.Identity.SignUp;
using FixFlow.Domain.Organizations;
using FluentValidation.Results;

namespace FixFlow.Application.Tests.Identity;

public class SignUpValidatorTests
{
    private readonly SignUpValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Theory]
    [InlineData(PasswordPolicy.MinLength - 1, false)]
    [InlineData(PasswordPolicy.MinLength, true)]
    [InlineData(PasswordPolicy.MaxLength, true)]
    [InlineData(PasswordPolicy.MaxLength + 1, false)]
    public void Validate_PasswordLength_AllowsMinToMax(int length, bool valid)
    {
        var result = _validator.Validate(Valid() with { Password = new string('a', length) });

        Assert.Equal(valid ? [] : [nameof(SignUpCommand.Password)], Failed(result));
    }

    [Fact]
    public void Validate_WhitespacePassword_Fails()
    {
        // Identity counts a whitespace-only password as too short, whatever its length
        var result = _validator.Validate(Valid() with { Password = new string(' ', PasswordPolicy.MinLength) });

        Assert.Equal([nameof(SignUpCommand.Password)], Failed(result));
    }

    [Theory]
    [InlineData("login")]
    [InlineData("Acme")]
    [InlineData(" acme")]
    [InlineData("ab")]
    public void Validate_InvalidSlug_FailsWithTheSlugRulesMessage(string slug)
    {
        Assert.False(OrganizationSlug.IsValid(slug, out var expected));

        var failure = Assert.Single(_validator.Validate(Valid() with { Slug = slug }).Errors);

        Assert.Equal(nameof(SignUpCommand.Slug), failure.PropertyName);
        Assert.Equal(expected, failure.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankNames_Fail(string name)
    {
        var result = _validator.Validate(Valid() with { FirstName = name, LastName = name });

        Assert.Equal([nameof(SignUpCommand.FirstName), nameof(SignUpCommand.LastName)], Failed(result));
    }

    [Fact]
    public void Validate_NameTooLongOnceTrimmed_Fails()
    {
        var result = _validator.Validate(Valid() with { FirstName = new string('a', User.NameMaxLength + 1) });

        Assert.Equal([nameof(SignUpCommand.FirstName)], Failed(result));
    }

    [Fact]
    public void Validate_PaddedValuesWithinLimitsOnceTrimmed_Pass()
    {
        var result = _validator.Validate(Valid() with
        {
            Email = "  user@example.test  ",
            FirstName = $" {new string('a', User.NameMaxLength)} ",
            OrganizationName = $" {new string('a', Organization.NameMaxLength)} ",
        });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@example.test")]
    [InlineData("user@")]
    [InlineData("user@@example.test")]
    // Passes the check only before trimming
    [InlineData(" @example.test")]
    // Identity rejects a line break; FluentValidation's check alone doesn't
    [InlineData("us\ner@example.test")]
    // A display name and another address, which mail would go to; Identity's check passes them
    [InlineData("Mallory <victim@example.test>")]
    [InlineData("Mallory<victim@example.test>")]
    [InlineData("<victim@example.test>")]
    [InlineData("user name@example.test")]
    // A comment, which mail drops, and forms the SMTP library can't read (the full list: ForgotPasswordValidatorTests)
    [InlineData("victim@example.test(Mallory)")]
    [InlineData("a,b@example.test")]
    [InlineData("a..b@example.test")]
    public void Validate_MalformedEmail_Fails(string email)
    {
        var result = _validator.Validate(Valid() with { Email = email });

        Assert.Equal([nameof(SignUpCommand.Email)], Failed(result));
    }

    [Fact]
    public void Validate_EmailLongerThanItsColumn_Fails()
    {
        var email = new string('a', User.EmailMaxLength - "@example.test".Length + 1) + "@example.test";

        var result = _validator.Validate(Valid() with { Email = email });

        Assert.Equal([nameof(SignUpCommand.Email)], Failed(result));
    }

    [Fact]
    public void Validate_EmailLongerThanItsColumnOnceNormalized_Fails()
    {
        // U+0958 has no composed form in NFC: it becomes U+0915 U+093C, which Identity stores as the normalized email
        var email = "क़" + new string('a', User.EmailMaxLength - "@example.test".Length - 1) + "@example.test";
        Assert.Equal(User.EmailMaxLength, email.Length);
        Assert.Equal(User.EmailMaxLength + 1, email.Normalize().Length);

        var result = _validator.Validate(Valid() with { Email = email });

        Assert.Equal([nameof(SignUpCommand.Email)], Failed(result));
    }

    [Theory]
    [InlineData("Acme\nRentals")]
    [InlineData("Acme\0Rentals")]
    public void Validate_OrganizationNameWithControlCharacter_Fails(string name)
    {
        var result = _validator.Validate(Valid() with { OrganizationName = name });

        Assert.Equal([nameof(SignUpCommand.OrganizationName)], Failed(result));
    }

    [Fact]
    public void Validate_MissingFields_FailOncePerFieldWithoutThrowing()
    {
        // What binding gives for a JSON body without the properties
        var result = _validator.Validate(new SignUpCommand(null!, null!, null!, null!, null!, null!));

        Assert.Equal(
            [
                nameof(SignUpCommand.Email),
                nameof(SignUpCommand.Password),
                nameof(SignUpCommand.FirstName),
                nameof(SignUpCommand.LastName),
                nameof(SignUpCommand.OrganizationName),
                nameof(SignUpCommand.Slug),
            ],
            Failed(result));
    }

    private static SignUpCommand Valid() =>
        new("user@example.test", "correct horse battery", "Test", "User", "Test Org", "test-org");

    private static List<string> Failed(ValidationResult result) => [.. result.Errors.Select(e => e.PropertyName)];
}
