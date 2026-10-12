using System.Reflection;

namespace FixFlow.Hosting;

public static class HostingAssembly
{
    public static Assembly Instance { get; } = typeof(HostingAssembly).Assembly;
}
