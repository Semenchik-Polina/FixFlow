namespace FixFlow.API.IntegrationTests.Fixtures;

public static partial class TestData
{
    /// <summary>Meets the password policy: long enough, nothing else required.</summary>
    public const string Password = "correct horse battery";

    /// <summary>A signup of unique values: a new email and a new slug. Vary it with <c>with</c>.</summary>
    public static SignUpRequest SignUp() =>
        new(Email(), Password, "Test", "User", Name("Org"), OrganizationSlug());
}

/// <summary>The body of <c>POST /auth/signup</c>, as the SPA sends it.</summary>
public sealed record SignUpRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string OrganizationName,
    string Slug);
