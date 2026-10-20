namespace FixFlow.API.Spa;

/// <summary>
/// Matches the SPA's client-side routes: every path outside the API, the health probes and the SignalR hubs (Phase 2,
/// ARCHITECTURE §13.2), so an unknown server route keeps its 401/404 (plan P10) instead of getting index.html.
/// Segment-aware and case-insensitive (<c>/apiary</c> is a client route, <c>/API/v1</c> isn't). T1.1 reserves these
/// names as organization slugs.
/// </summary>
internal sealed class ClientRouteConstraint : IRouteConstraint
{
    public const string Name = "clientRoute";

    private static readonly PathString[] _serverPaths = ["/api", "/health", "/hubs"];

    public bool Match(
        HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection) =>
        // No value: the root path
        values[routeKey] is not string path
        || !Array.Exists(_serverPaths, serverPath => new PathString($"/{path}").StartsWithSegments(serverPath));
}
