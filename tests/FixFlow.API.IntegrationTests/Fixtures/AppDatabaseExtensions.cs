using FixFlow.Application.Auth;
using FixFlow.Application.Identity;
using FixFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>
/// The app's own database, for what no endpoint does yet (suspending a member, adding one to an organization) and for
/// checks behind the API. Unlike <see cref="FixFlowApiFactory.Persistence"/>, which is a database of its own.
/// </summary>
public static class AppDatabaseExtensions
{
    /// <summary>
    /// One unit of work in <paramref name="organizationId"/>'s context, through the trusted scope that signup uses: the
    /// organization filter and the ownership checks apply as they do in a request. Test code may open the scope; the app
    /// only from allow-listed handlers.
    /// </summary>
    public static async Task<T> InOrganizationAsync<T>(
        this FixFlowApiFactory api, Guid organizationId, Func<FixFlowDbContext, Task<T>> work)
    {
        await using var scope = api.Services.CreateAsyncScope();
        using var organization = scope.ServiceProvider.GetRequiredService<ITrustedOrganizationScope>().Open(organizationId);
        return await work(scope.ServiceProvider.GetRequiredService<FixFlowDbContext>());
    }

    /// <summary>The account with this email as Identity finds it (normalized), or null.</summary>
    public static async Task<User?> FindUserAsync(this FixFlowApiFactory api, string email)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<User>>().FindByEmailAsync(email);
    }
}
