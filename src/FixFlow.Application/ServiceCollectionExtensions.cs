using System.Reflection;
using FixFlow.Application.Auth;
using FixFlow.Application.Events;
using FixFlow.Application.Organizations;
using FixFlow.Application.Organizations.Context;
using FixFlow.Application.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FixFlow.Application;

public static class ServiceCollectionExtensions
{
    private static readonly Type[] _eventHandlerInterfaces =
        [typeof(IInTransactionEventHandler<>), typeof(IPostCommitEventHandler<>)];

    /// <summary>
    /// MediatR handlers with the validation pipeline, the FluentValidation validators, the domain event handlers, the
    /// resource authorization handlers and the services that handlers share.
    /// </summary>
    public static IServiceCollection AddFixFlowApplication(this IServiceCollection services)
    {
        // English messages whatever the server's culture (D27)
        ValidatorOptions.Global.LanguageManager.Enabled = false;

        services.AddMediatR(config => config
            .RegisterServicesFromAssembly(ApplicationAssembly.Instance)
            .AddOpenBehavior(typeof(ValidationBehavior<,>)));
        services.AddValidatorsFromAssembly(ApplicationAssembly.Instance, includeInternalTypes: true);
        services.AddDomainEventHandlers(ApplicationAssembly.Instance);
        services.AddResourceAuthorizationHandlers(ApplicationAssembly.Instance);
        services.AddScoped<IResourceAuthorizer, ResourceAuthorizer>();

        services.AddScoped<OrganizationCreator>();
        // Needs a HybridCache, which the host registers (Infrastructure's AddFixFlowCaching)
        services.AddScoped<ActiveMembershipResolver>();

        return services;
    }

    /// <summary>
    /// Registers every class in the assembly that implements <see cref="IInTransactionEventHandler{TEvent}"/> or
    /// <see cref="IPostCommitEventHandler{TEvent}"/>, once per event it handles. Transient: the dispatcher resolves
    /// them from the request's scope, so they share its DbContext.
    /// </summary>
    public static IServiceCollection AddDomainEventHandlers(this IServiceCollection services, Assembly assembly)
    {
        foreach (var (serviceType, handler) in Implementations(assembly, _eventHandlerInterfaces))
        {
            services.TryAddEnumerable(ServiceDescriptor.Transient(serviceType, handler));
        }

        return services;
    }

    /// <summary>
    /// Registers every class in the assembly that implements <see cref="IResourceAuthorizationHandler{TResource}"/>, for
    /// <see cref="IResourceAuthorizer"/>. Scoped, like the current user and the DbContext they read.
    /// </summary>
    public static IServiceCollection AddResourceAuthorizationHandlers(this IServiceCollection services, Assembly assembly)
    {
        foreach (var (serviceType, handler) in Implementations(assembly, typeof(IResourceAuthorizationHandler<>)))
        {
            services.TryAddEnumerable(ServiceDescriptor.Scoped(serviceType, handler));
        }

        return services;
    }

    /// <summary>The closed interfaces of <paramref name="openInterfaces"/> that the assembly's classes implement.</summary>
    private static IEnumerable<(Type ServiceType, Type Implementation)> Implementations(
        Assembly assembly, params Type[] openInterfaces) =>
        assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .SelectMany(type => type
                .GetInterfaces()
                .Where(i => i.IsGenericType && openInterfaces.Contains(i.GetGenericTypeDefinition()))
                .Select(i => (i, type)));
}
