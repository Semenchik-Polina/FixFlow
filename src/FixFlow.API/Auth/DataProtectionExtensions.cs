using Microsoft.AspNetCore.DataProtection;

namespace FixFlow.API.Auth;

public static class DataProtectionExtensions
{
    /// <summary>Ties every instance to one key ring; by default it would be the content root's path.</summary>
    public const string ApplicationName = "FixFlow";

    /// <summary>The folder that holds the keys. Optional: unset, the framework's default folder applies.</summary>
    public const string KeysPathSetting = "DataProtection:KeysPath";

    /// <summary>
    /// The keys that protect the auth cookie (plan P24). With <see cref="KeysPathSetting"/> set, they live in that
    /// folder. The <c>full</c> profile mounts a named volume there, so a recreated container, or another replica on the
    /// same host, keeps reading the cookies already issued. Unset (the API on the host, tests), the framework's default
    /// applies, which outlives the process there. The keys aren't encrypted at rest in the container: accepted while
    /// FixFlow runs locally only (D20), and revisited with hosting.
    /// </summary>
    public static IServiceCollection AddFixFlowDataProtection(
        this IServiceCollection services, IConfiguration configuration)
    {
        var dataProtection = services.AddDataProtection().SetApplicationName(ApplicationName);

        var keysPath = configuration[KeysPathSetting];
        if (!string.IsNullOrEmpty(keysPath))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        return services;
    }
}
