using System.Reflection;
using FixFlow.Application.Events;
using FixFlow.Application.Organizations;
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
    /// MediatR handlers with the validation pipeline, the FluentValidation validators, the domain event handlers and
    /// the services that handlers share.
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

        services.AddScoped<OrganizationCreator>();

        return services;
    }

    /// <summary>
    /// Registers every class in the assembly that implements <see cref="IInTransactionEventHandler{TEvent}"/> or
    /// <see cref="IPostCommitEventHandler{TEvent}"/>, once per event it handles. Transient: the dispatcher resolves
    /// them from the request's scope, so they share its DbContext.
    /// </summary>
    public static IServiceCollection AddDomainEventHandlers(this IServiceCollection services, Assembly assembly)
    {
        var handlers = assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

        foreach (var handler in handlers)
        {
            var handled = handler
                .GetInterfaces()
                .Where(i => i.IsGenericType && _eventHandlerInterfaces.Contains(i.GetGenericTypeDefinition()));

            foreach (var serviceType in handled)
            {
                services.TryAddEnumerable(ServiceDescriptor.Transient(serviceType, handler));
            }
        }

        return services;
    }
}
