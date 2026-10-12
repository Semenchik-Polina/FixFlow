namespace FixFlow.Domain.Tests;

public class ArchitectureTests
{
    // Domain may depend on the BCL, NodaTime and Stateless only (src/CLAUDE.md §1)
    private static readonly string[] _allowed = ["System", "netstandard", "NodaTime", "Stateless"];

    [Fact]
    public void Domain_References_OnlyBclNodaTimeAndStateless()
    {
        var forbidden = DomainAssembly.Instance
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => !_allowed.Any(a => name == a || name.StartsWith(a + ".", StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(forbidden);
    }

    // CI check (T0.5), never merged: a failing test must fail the Test step
    [Fact]
    public void Ci_Check_FailsOnPurpose() => Assert.Fail("CI check: this test fails on purpose (second push)");
}
