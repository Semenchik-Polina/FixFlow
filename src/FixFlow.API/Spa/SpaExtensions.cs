using Microsoft.Extensions.FileProviders;

namespace FixFlow.API.Spa;

/// <summary>
/// Serves the built SPA from wwwroot, where the image copies it (ARCHITECTURE §16, plan T0.11). On the host Vite serves
/// the SPA and wwwroot has no index.html, so nothing is served.
/// </summary>
public static class SpaExtensions
{
    private const string IndexFile = "index.html";

    // Vite's content-hashed bundle: a changed file gets a new name
    private const string ImmutableCaching = "public, max-age=31536000, immutable";

    // index.html and the unhashed files next to it name the current bundle, so the browser revalidates them
    private const string Revalidate = "no-cache";

    private static readonly PathString _assetsPath = "/assets";

    // Paths that look like files never fall back, so a missing asset isn't answered with index.html
    private const string ClientRoutePattern = $"{{*path:nonfile:{ClientRouteConstraint.Name}}}";

    public static IServiceCollection AddFixFlowSpa(this IServiceCollection services) =>
        services.Configure<RouteOptions>(options =>
            options.SetParameterPolicy<ClientRouteConstraint>(ClientRouteConstraint.Name));

    /// <summary>The SPA's bundle. Register it before authentication: the bundle is public, and the SPA signs in itself.</summary>
    public static IApplicationBuilder UseFixFlowSpaStaticFiles(this WebApplication app)
    {
        if (GetIndex(app.Environment) is not null)
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = context => context.Context.Response.Headers.CacheControl =
                    context.Context.Request.Path.StartsWithSegments(_assetsPath) ? ImmutableCaching : Revalidate,
            });
        }

        return app;
    }

    /// <summary>
    /// index.html for deep links such as <c>/acme/requests</c>, so the SPA's router can take over. Unlike
    /// <c>MapFallbackToFile</c>, it leaves the request path alone, so logs and traces show the requested route.
    /// </summary>
    public static IEndpointRouteBuilder MapFixFlowSpa(this WebApplication app)
    {
        if (GetIndex(app.Environment) is { } index)
        {
            app.MapFallback(ClientRoutePattern, context => ServeIndexAsync(context, index)).AllowAnonymous();
        }

        return app;
    }

    private static Task ServeIndexAsync(HttpContext context, IFileInfo index)
    {
        var method = context.Request.Method;
        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
        {
            // As the static file middleware answers: a client route can't be posted to
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength = index.Length;
        context.Response.Headers.CacheControl = Revalidate;

        return HttpMethods.IsHead(method) ? Task.CompletedTask : context.Response.SendFileAsync(index, context.RequestAborted);
    }

    private static IFileInfo? GetIndex(IWebHostEnvironment environment) =>
        environment.WebRootFileProvider.GetFileInfo(IndexFile) is { Exists: true } index ? index : null;
}
