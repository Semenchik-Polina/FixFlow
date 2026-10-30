using FixFlow.API.Organizations;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FixFlow.API.Endpoints;

public static class EndpointModuleExtensions
{
    public const string ApiRoot = "/api/v1";

    /// <summary>Registers a module for <see cref="MapFixFlowApi"/>. Tests register their own test-only modules.</summary>
    public static IServiceCollection AddEndpointModule<T>(this IServiceCollection services)
        where T : class, IEndpointModule
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEndpointModule, T>());
        return services;
    }

    /// <summary>
    /// The <c>/api/v1</c> root with one group per registered module. Its endpoints need an organization context unless
    /// their module or they themselves are marked <see cref="OrganizationContextExtensions.WithoutOrganization{TBuilder}"/>.
    /// </summary>
    public static RouteGroupBuilder MapFixFlowApi(this IEndpointRouteBuilder endpoints)
    {
        // The default, so an endpoint that doesn't choose fails closed: it answers 400 without the header rather than
        // run without an organization
        var api = endpoints.MapGroup(ApiRoot).RequireOrganization();

        foreach (var module in endpoints.ServiceProvider.GetServices<IEndpointModule>())
        {
            module.Map(api.MapGroup(module.Prefix));
        }

        return api;
    }
}
