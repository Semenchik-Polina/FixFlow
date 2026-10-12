using System.Reflection;

namespace FixFlow.Infrastructure;

public static class InfrastructureAssembly
{
    public static Assembly Instance { get; } = typeof(InfrastructureAssembly).Assembly;
}
