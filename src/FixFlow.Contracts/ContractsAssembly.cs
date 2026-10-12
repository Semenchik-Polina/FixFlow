using System.Reflection;

namespace FixFlow.Contracts;

public static class ContractsAssembly
{
    public static Assembly Instance { get; } = typeof(ContractsAssembly).Assembly;
}
