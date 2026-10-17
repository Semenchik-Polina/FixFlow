namespace FixFlow.API.Endpoints;

/// <summary>
/// One module's endpoints (work orders, requests, ...). Registered with
/// <see cref="EndpointModuleExtensions.AddEndpointModule{T}"/> and mapped by
/// <see cref="EndpointModuleExtensions.MapFixFlowApi"/> under <c>/api/v1{Prefix}</c>, one <c>MapGroup</c> per module.
/// </summary>
public interface IEndpointModule
{
    /// <summary>The module's path under <c>/api/v1</c>, e.g. <c>/work-orders</c>.</summary>
    string Prefix { get; }

    /// <summary>
    /// Maps the module's endpoints. Each declares its policy (or an allow-listed <c>AllowAnonymous</c>) and its
    /// responses (<c>ProducesProblem</c>, ...), so the OpenAPI document and the generated client stay accurate.
    /// </summary>
    void Map(RouteGroupBuilder group);
}
