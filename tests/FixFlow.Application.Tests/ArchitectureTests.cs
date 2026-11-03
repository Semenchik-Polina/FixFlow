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

    /// <summary>
    /// Application reads the caller and the organization only through <c>ICurrentUser</c> (§8), never from the request,
    /// so it has no <c>HttpContext</c>: no ASP.NET Core assembly at all. Identity's <c>Microsoft.AspNetCore.Identity</c>
    /// namespace isn't one; its types live in <c>Microsoft.Extensions.Identity.*</c> (plan P22).
    /// </summary>
    [Fact]
    public void Application_References_NoAspNetCoreAssembly()
    {
        var references = ApplicationAssembly.Instance.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        // The check reads real references: Identity's, which the handlers use, are among them
        Assert.Contains("Microsoft.Extensions.Identity.Core", references);
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal));
    }
}
