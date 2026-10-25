using System.Reflection;
using System.Runtime.CompilerServices;
using FixFlow.Application;
using FixFlow.Application.Auth;
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

    /// <summary>
    /// An open trusted scope decides which organization every write goes to (plan P25), so only the types listed here
    /// may take it, whether through its interface or its class, in a constructor or a method. An endpoint's delegate
    /// is a method of a compiler-generated class, so it counts as the type that declares that class. Names, not types:
    /// the handlers are internal. Only this project sees all three assemblies.
    /// </summary>
    [Fact]
    public void TrustedOrganizationScope_UsedOnlyByAllowListedTypes()
    {
        string[] allowed =
        [
            // Open it for the organization they have just created. T3.2 adds AcceptInviteHandler.
            "FixFlow.Application.Identity.SignUp.SignUpHandler",
            "FixFlow.Application.Organizations.CreateOrganization.CreateOrganizationHandler",
            // Takes the class, not the interface, to read the open id
            "FixFlow.API.Auth.HttpCurrentUser",
        ];
        Assembly[] assemblies = [ApplicationAssembly.Instance, InfrastructureAssembly.Instance, typeof(Program).Assembly];
        const BindingFlags declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
            | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        var violations = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            // The scope's own types, such as the handle that closes it
            .Where(type => !typeof(ITrustedOrganizationScope).IsAssignableFrom(type.DeclaringType ?? type))
            .Where(type => type
                .GetConstructors(declared)
                .Concat<MethodBase>(type.GetMethods(declared))
                .SelectMany(method => method.GetParameters())
                .Any(parameter => typeof(ITrustedOrganizationScope).IsAssignableFrom(parameter.ParameterType)))
            .Select(type => SourceType(type).FullName)
            .Where(name => !allowed.Contains(name));

        Assert.Empty(violations);
    }

    /// <summary>The type that a compiler-generated one (a lambda's closure, a state machine) was generated for.</summary>
    private static Type SourceType(Type type)
    {
        while (type.DeclaringType is not null && type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
        {
            type = type.DeclaringType;
        }

        return type;
    }
}
