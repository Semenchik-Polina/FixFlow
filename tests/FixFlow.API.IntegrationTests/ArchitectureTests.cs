using System.Reflection;
using FixFlow.Contracts;
using FixFlow.Hosting;
using FixFlow.Infrastructure;

namespace FixFlow.API.IntegrationTests;

public class ArchitectureTests
{
    // Allowed FixFlow references per src/CLAUDE.md §1 (Domain and Application are checked in their own test projects)
    public static TheoryData<string, string[]> AllowedReferences => new()
    {
        { "FixFlow.Infrastructure", ["FixFlow.Application", "FixFlow.Domain"] },
        { "FixFlow.Contracts", [] },
        { "FixFlow.Hosting", [] },
    };

    private static readonly Assembly[] _assemblies =
    [
        InfrastructureAssembly.Instance,
        ContractsAssembly.Instance,
        HostingAssembly.Instance,
    ];

    [Theory]
    [MemberData(nameof(AllowedReferences))]
    public void Project_References_OnlyAllowedFixFlowProjects(string project, string[] allowed)
    {
        var assembly = _assemblies.Single(a => a.GetName().Name == project);

        var forbidden = assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => name.StartsWith("FixFlow.", StringComparison.Ordinal) && !allowed.Contains(name))
            .ToList();

        Assert.Empty(forbidden);
    }
}
