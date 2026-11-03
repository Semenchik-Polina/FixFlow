using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>
/// Holds the factories of the host's <see cref="HybridCache"/> until the test releases them, so a lookup stays in flight
/// while another caller of its key joins it: the callers of one key share one factory. It tells when a factory is held
/// and when a second caller has joined. Installed on a host of its own
/// (<see cref="CacheFactoryGateExtensions.WithCacheFactoryGate"/>).
/// </summary>
public sealed class CacheFactoryGate
{
    private readonly TaskCompletionSource _held = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _joined = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _callers;

    /// <summary>Completes when a factory waits at the gate.</summary>
    public Task Held => _held.Task;

    /// <summary>Completes when a second caller waits for the cache; for the held factory's key, it has joined it.</summary>
    public Task Joined => _joined.Task;

    /// <summary>Lets the held factory, and every later one, run.</summary>
    public void Release() => _released.TrySetResult();

    internal sealed class GatedCache(HybridCache inner, CacheFactoryGate gate) : HybridCache
    {
        public override ValueTask<T> GetOrCreateAsync<TState, T>(
            string key,
            TState state,
            Func<TState, CancellationToken, ValueTask<T>> factory,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            var result = inner.GetOrCreateAsync(
                key,
                (state, factory, gate),
                static async (held, ct) =>
                {
                    held.gate._held.TrySetResult();
                    await held.gate._released.Task;
                    return await held.factory(held.state, ct);
                },
                options,
                tags,
                cancellationToken);

            // The inner cache has joined this caller to a factory in flight by the time it returns
            if (Interlocked.Increment(ref gate._callers) == 2)
            {
                gate._joined.TrySetResult();
            }

            return result;
        }

        public override ValueTask SetAsync<T>(
            string key,
            T value,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default) =>
            inner.SetAsync(key, value, options, tags, cancellationToken);

        public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            inner.RemoveAsync(key, cancellationToken);

        public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) =>
            inner.RemoveByTagAsync(tag, cancellationToken);
    }
}

public static class CacheFactoryGateExtensions
{
    /// <summary>
    /// A host of its own, on the run's containers, whose <see cref="HybridCache"/> is the app's, behind
    /// <paramref name="gate"/>.
    /// </summary>
    public static WebApplicationFactory<Program> WithCacheFactoryGate(this FixFlowApiFactory api, CacheFactoryGate gate) =>
        api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            var registered = services.Single(service => service.ServiceType == typeof(HybridCache));
            var implementation = registered.ImplementationType
                ?? throw new InvalidOperationException("AddHybridCache no longer registers HybridCache by its type.");
            services.Remove(registered);
            services.AddSingleton<HybridCache>(provider => new CacheFactoryGate.GatedCache(
                (HybridCache)ActivatorUtilities.CreateInstance(provider, implementation), gate));
        }));
}
