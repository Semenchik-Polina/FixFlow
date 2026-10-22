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

    /// <summary>The <c>/api/v1</c> root with one group per registered module.</summary>
    public static RouteGroupBuilder MapFixFlowApi(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup(ApiRoot);

        foreach (var module in endpoints.ServiceProvider.GetServices<IEndpointModule>())
        {
            module.Map(api.MapGroup(module.Prefix));
        }

        return api;
    }
}
