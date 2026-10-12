using System.Reflection;

namespace FixFlow.Domain;

public static class DomainAssembly
{
    public static Assembly Instance { get; } = typeof(DomainAssembly).Assembly;

    // CI check (T0.5), never merged: an unused local (CS0219) must fail the Build step
    private static void CiCheck() { var unused = 1; }
}
