using FixFlow.API.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FixFlow.API.OpenApi;

/// <summary>
/// An endpoint guarded by one of the role <see cref="Policies"/> answers 403 Problem Details to a member without the
/// role. This declares it, as <c>ProducesProblem</c> would, so the client knows it.
/// </summary>
internal sealed class RolePolicyResponsesTransformer : IOpenApiOperationTransformer
{
    private static readonly int[] _statusCodes = [StatusCodes.Status403Forbidden];

    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var hasRolePolicy = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>()
            .Any(data => data.Policy is { } policy && Policies.Roles.ContainsKey(policy));

        return hasRolePolicy
            ? ProblemResponses.TryAddAsync(operation, context, _statusCodes, cancellationToken)
            : Task.CompletedTask;
    }
}
