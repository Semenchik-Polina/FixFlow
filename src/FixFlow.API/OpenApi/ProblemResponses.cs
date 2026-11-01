using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.OpenApi;

namespace FixFlow.API.OpenApi;

/// <summary>Problem Details responses that operation transformers declare for an endpoint, as <c>ProducesProblem</c> would.</summary>
internal static class ProblemResponses
{
    private const string ProblemSchemaId = nameof(ProblemDetails);

    /// <summary>
    /// Adds a Problem Details response per status code, unless the operation already declares that status itself (a
    /// validation problem's 400, its own 404). Registers the <c>ProblemDetails</c> component if no operation has yet.
    /// </summary>
    public static async Task TryAddAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        IEnumerable<int> statusCodes,
        CancellationToken cancellationToken)
    {
        if (context.Document is not { } document)
        {
            return;
        }

        if (document.Components?.Schemas?.ContainsKey(ProblemSchemaId) != true)
        {
            document.AddComponent(
                ProblemSchemaId, await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), null, cancellationToken));
        }

        operation.Responses ??= [];
        foreach (var statusCode in statusCodes)
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
