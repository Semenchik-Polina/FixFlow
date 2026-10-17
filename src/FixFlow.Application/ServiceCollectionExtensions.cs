using FixFlow.Application.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.Application;

public static class ServiceCollectionExtensions
{
    /// <summary>MediatR handlers with the validation pipeline, and the FluentValidation validators.</summary>
    public static IServiceCollection AddFixFlowApplication(this IServiceCollection services)
    {
        // English messages whatever the server's culture (D27)
        ValidatorOptions.Global.LanguageManager.Enabled = false;

        services.AddMediatR(config => config
            .RegisterServicesFromAssembly(ApplicationAssembly.Instance)
            .AddOpenBehavior(typeof(ValidationBehavior<,>)));
        services.AddValidatorsFromAssembly(ApplicationAssembly.Instance, includeInternalTypes: true);

        return services;
    }
}
