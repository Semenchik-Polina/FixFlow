using FixFlow.API.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.API.IntegrationTests.Identity;

public class DataProtectionTests
{
    private const string Purpose = "data-protection-tests";

    [Fact]
    public void Protect_OnOneInstance_UnprotectsOnAnother()
    {
        // A folder of its own, like the full profile's volume
        var keys = Directory.CreateTempSubdirectory("fixflow-keys-");
        try
        {
            using var first = Instance(keys);
            using var second = Instance(keys);

            // A recreated container, or a second replica: the same folder, nothing shared in memory
            var payload = first.GetDataProtector(Purpose).Protect("session");

            Assert.Equal("session", second.GetDataProtector(Purpose).Unprotect(payload));
            Assert.Single(keys.GetFiles("key-*.xml"));
        }
        finally
        {
            keys.Delete(recursive: true);
        }
    }

    /// <summary>One instance of the API, as far as its key ring goes.</summary>
    private static ServiceProvider Instance(DirectoryInfo keys) =>
        new ServiceCollection()
            .AddLogging()
            .AddFixFlowDataProtection(new ConfigurationBuilder()
                .AddInMemoryCollection([new(DataProtectionExtensions.KeysPathSetting, keys.FullName)])
                .Build())
            .BuildServiceProvider();
}
