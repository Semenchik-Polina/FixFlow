using FixFlow.API.Organizations;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FixFlow.API.OpenApi;

/// <summary>
/// An endpoint that needs an organization context (<see cref="OrganizationContextMetadata"/>) can also answer from
/// <see cref="OrganizationContextMiddleware"/>: 400 for a missing, empty or repeated header and 404 for every miss, both
/// Problem Details. This declares them, as <c>ProducesProblem</c> would, so the client knows them. Group metadata can't
/// do it: an organization-less endpoint couldn't take the root's responses back. A response the endpoint declares
/// itself (a validation problem's 400, its own 404) stays as it is. The header isn't a documented parameter: the SPA's
/// HTTP client sends it on every call (plan T1.5).
/// </summary>
internal sealed class OrganizationContextResponsesTransformer : IOpenApiOperationTransformer
{
    private static readonly int[] _statusCodes = [StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound];

    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        // The last one wins, as in the middleware (GetMetadata)
        var metadata = context.Description.ActionDescriptor.EndpointMetadata.OfType<OrganizationContextMetadata>().LastOrDefault();

        return metadata is { IsRequired: true }
            ? ProblemResponses.TryAddAsync(operation, context, _statusCodes, cancellationToken)
            : Task.CompletedTask;
    }
}
