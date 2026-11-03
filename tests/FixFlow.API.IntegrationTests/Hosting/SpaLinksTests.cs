using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.Spa;
using FixFlow.Application.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FixFlow.API.IntegrationTests.Hosting;

public class SpaLinksTests(FixFlowApiFactory api)
{
    [Fact]
    public void ResetPassword_App_LinksToPublicUrlWithTheTokenInTheFragment()
    {
        var links = api.Services.GetRequiredService<ISpaLinks>();

        // A code's characters are URL-safe, so they stay as they are
        var link = links.ResetPassword("user-id.reset_code");

        Assert.Equal("https://fixflow.example.test/reset-password#token=user-id.reset_code", link.AbsoluteUri);
        Assert.Empty(link.Query);
    }

    [Theory]
    [InlineData("https://fixflow.example.test/app")]
    [InlineData("https://fixflow.example.test/app/")]
    public void ResetPassword_PublicUrlWithPath_KeepsThePath(string publicUrl)
    {
        using var services = Services(publicUrl);

        var link = services.GetRequiredService<ISpaLinks>().ResetPassword("code");

        Assert.Equal("https://fixflow.example.test/app/reset-password#token=code", link.AbsoluteUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("fixflow.example.test")]
    [InlineData("/reset")]
    [InlineData("ftp://fixflow.example.test")]
    [InlineData("https://fixflow.example.test/?tenant=a")]
    [InlineData("https://fixflow.example.test/#home")]
    public void ResetPassword_InvalidPublicUrl_FailsOnFirstUse(string? publicUrl)
    {
        using var services = Services(publicUrl);
        var links = services.GetRequiredService<ISpaLinks>();

        Assert.Throws<OptionsValidationException>(() => links.ResetPassword("code"));
    }

    private static ServiceProvider Services(string? publicUrl)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Spa:PublicUrl"] = publicUrl })
            .Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddFixFlowSpa()
            .BuildServiceProvider();
    }
}
