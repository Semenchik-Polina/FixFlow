using System.Reflection;

namespace FixFlow.API.OpenApi;

/// <summary>
/// <c>dotnet build</c> runs the app through GetDocument.Insider to write the OpenAPI document. That run has no
/// connection strings or secrets (in CI or anywhere else), so Program skips what needs them.
/// </summary>
internal static class BuildTimeDocument
{
    public static bool IsGenerating { get; } =
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
