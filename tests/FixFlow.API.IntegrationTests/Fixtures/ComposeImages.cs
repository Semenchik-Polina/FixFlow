namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>
/// The image tags from <c>docker-compose.yml</c> (copied to the test output), so the tests always run on the same
/// images as local development and a Dependabot bump in Compose is tested on its own PR (plan P6).
/// </summary>
internal static class ComposeImages
{
    public const string Postgres = "postgres";
    public const string Azurite = "mcr.microsoft.com/azure-storage/azurite";

    private static readonly Lazy<string[]> _images = new(ReadImages);

    public static string ComposeDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Compose");

    /// <summary>The full <c>repository:tag</c> for an image repository used in Compose.</summary>
    public static string Get(string repository) =>
        _images.Value.SingleOrDefault(image => image.StartsWith(repository + ":", StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"docker-compose.yml has no 'image: {repository}:<tag>' line.");

    private static string[] ReadImages() =>
        File.ReadLines(Path.Combine(ComposeDirectory, "docker-compose.yml"))
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("image:", StringComparison.Ordinal))
            .Select(line => line["image:".Length..].Trim())
            .ToArray();
}
