namespace FixFlow.Application.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Application_References_NoFixFlowProjectExceptDomain()
    {
        var forbidden = ApplicationAssembly.Instance
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => name.StartsWith("FixFlow.", StringComparison.Ordinal) && name != "FixFlow.Domain")
            .ToList();

        Assert.Empty(forbidden);
    }
}
