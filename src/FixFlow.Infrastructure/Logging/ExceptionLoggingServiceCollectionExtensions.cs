using FixFlow.Application.Errors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FixFlow.Infrastructure.Logging;

public static class ExceptionLoggingServiceCollectionExtensions
{
    /// <summary>
    /// <see cref="IExceptionRedactor"/> (ADR 0007). <paramref name="keepMessages"/> logs exceptions as thrown: the API
    /// passes it in Development only, where the data is test data (S21). Without this call, persistence registers the
    /// redactor itself, and it removes messages.
    /// </summary>
    public static IServiceCollection AddFixFlowExceptionLogging(this IServiceCollection services, bool keepMessages)
    {
        services.Configure<ExceptionLoggingOptions>(options => options.KeepMessages = keepMessages);
        services.TryAddSingleton<IExceptionRedactor, ExceptionRedactor>();
        return services;
    }
}
