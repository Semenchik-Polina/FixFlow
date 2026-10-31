using System.Globalization;
using FixFlow.API.Organizations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.WebUtilities;
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
    private const string ProblemSchemaId = nameof(ProblemDetails);

    private static readonly int[] _statusCodes = [StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound];

    public async Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        // The last one wins, as in the middleware (GetMetadata)
        var metadata = context.Description.ActionDescriptor.EndpointMetadata.OfType<OrganizationContextMetadata>().LastOrDefault();
        if (metadata is not { IsRequired: true } || context.Document is not { } document)
        {
            return;
        }

        if (document.Components?.Schemas?.ContainsKey(ProblemSchemaId) != true)
        {
            document.AddComponent(
                ProblemSchemaId, await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), null, cancellationToken));
        }

        operation.Responses ??= [];
        foreach (var statusCode in _statusCodes)
        {
            operation.Responses.TryAdd(statusCode.ToString(CultureInfo.InvariantCulture), new OpenApiResponse
            {
                Description = ReasonPhrases.GetReasonPhrase(statusCode),
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new()
                    {
                        Schema = new OpenApiSchemaReference(ProblemSchemaId, document),
                    },
                },
            });
        }
    }
}
