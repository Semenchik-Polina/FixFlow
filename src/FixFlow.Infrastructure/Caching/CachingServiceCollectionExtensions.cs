using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.Infrastructure.Caching;

public static class CachingServiceCollectionExtensions
{
    /// <summary>
    /// <see cref="HybridCache"/> in this process's memory only (plan A7): tag invalidation reaches this instance only.
    /// Phase 2 adds Redis as the second level here (§13.1), with no change to the code that uses the cache.
    /// </summary>
    public static IServiceCollection AddFixFlowCaching(this IServiceCollection services)
    {
        services.AddHybridCache();

        return services;
    }
}
