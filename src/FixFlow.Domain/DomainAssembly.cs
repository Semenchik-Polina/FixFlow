using System.Reflection;

namespace FixFlow.Domain;

public static class DomainAssembly
{
    public static Assembly Instance { get; } = typeof(DomainAssembly).Assembly;
}
