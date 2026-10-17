namespace FixFlow.API.Errors;

public static class ErrorHandlingExtensions
{
    /// <summary>RFC 9457 Problem Details (with <c>traceId</c>) for every error response.</summary>
    public static IServiceCollection AddFixFlowErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<ProblemExceptionHandler>();
        return services;
    }

    /// <summary>
    /// Exceptions → Problem Details, and a Problem Details body for error responses that have none (401 from the
    /// fallback policy, 404 for an unknown route, 400 from binding).
    /// </summary>
    public static IApplicationBuilder UseFixFlowErrorHandling(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }
}
