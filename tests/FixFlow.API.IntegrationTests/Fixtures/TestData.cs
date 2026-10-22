using System.Security.Cryptography;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>
/// Unique fake values (D23: <c>*.example.test</c>, generated names). Every test creates its own organizations and
/// users from these, which keeps parallel tests apart without resetting the database (plan A12).
/// Each slice adds its builders in its own file (<c>TestData.Organizations.cs</c>, ...).
/// </summary>
public static partial class TestData
{
    public static string Email() => $"user-{Token()}@example.test";

    public static string OrganizationSlug() => $"org-{Token()}";

    public static string Name(string prefix) => $"{prefix} {Token()}";

    private static string Token() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
}
