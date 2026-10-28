using System.Globalization;
using FixFlow.Application.Errors;
using FixFlow.Application.Persistence;
using FixFlow.Infrastructure.Logging;
using FixFlow.Infrastructure.Persistence.Events;
using FixFlow.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FixFlow.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// <see cref="FixFlowDbContext"/> on PostgreSQL with NodaTime and snake_case names, behind
    /// <see cref="IFixFlowDbContext"/>. The connection string is resolved when the first context is created, so a run
    /// that never opens one (the build-time OpenAPI document) needs no database settings.
    /// </summary>
    public static IServiceCollection AddFixFlowPersistence(
        this IServiceCollection services, Func<IServiceProvider, string> connectionString) =>
        services.AddFixFlowPersistence<FixFlowDbContext>(connectionString);

    /// <summary>The same, for a context derived from <see cref="FixFlowDbContext"/> (tests add entities that way).</summary>
    public static IServiceCollection AddFixFlowPersistence<TContext>(
        this IServiceCollection services, Func<IServiceProvider, string> connectionString)
        where TContext : FixFlowDbContext
    {
        string? resolvedConnectionString = null;

        services.AddScoped<DomainEventDispatcher>();
        // The dispatcher logs failed post-commit handlers through it. A host that doesn't call
        // AddFixFlowExceptionLogging (tests, tools) gets messages removed, the safe default (ADR 0007).
        services.TryAddSingleton<IExceptionRedactor, ExceptionRedactor>();
        services.AddScoped<OrganizationOwnershipInterceptor>();
        services.AddScoped<AuditInterceptor>();
        services.AddSingleton<DomainEventsTransactionInterceptor>();

        services.AddDbContext<TContext>((provider, options) => options
            .UseNpgsql(
                resolvedConnectionString ??= connectionString(provider),
                npgsql => npgsql
                    .UseNodaTime()
                    .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Migrations))
            // Not the server's culture: names must be the same everywhere
            .UseSnakeCaseNamingConvention(CultureInfo.InvariantCulture)
            .AddInterceptors(
                provider.GetRequiredService<OrganizationOwnershipInterceptor>(),
                provider.GetRequiredService<AuditInterceptor>(),
                provider.GetRequiredService<DomainEventsTransactionInterceptor>()));

        services.AddScoped<IFixFlowDbContext>(provider => provider.GetRequiredService<TContext>());
        if (typeof(TContext) != typeof(FixFlowDbContext))
        {
            services.AddScoped<FixFlowDbContext>(provider => provider.GetRequiredService<TContext>());
        }

        return services;
    }

    /// <summary>Applies the pending migrations. Only Development runs it on startup (§20); tests call it once per run.</summary>
    public static async Task MigrateFixFlowDatabaseAsync(
        this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<FixFlowDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
