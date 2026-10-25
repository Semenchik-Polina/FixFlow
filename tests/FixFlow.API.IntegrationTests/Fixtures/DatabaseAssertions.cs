using FixFlow.Application.Errors;

namespace FixFlow.API.IntegrationTests.Fixtures;

public static class DatabaseAssertions
{
    /// <summary>
    /// Asserts that <paramref name="save"/> fails on <paramref name="constraint"/>, a unique index. Handlers turn such
    /// a violation into a field error by the constraint's name (plan P27), so the name is part of the contract.
    /// </summary>
    public static async Task AssertUniqueViolationAsync(string constraint, Func<Task> save)
    {
        var exception = await Assert.ThrowsAsync<UniqueConstraintViolationException>(save);

        Assert.Equal(constraint, exception.ConstraintName);
    }
}
