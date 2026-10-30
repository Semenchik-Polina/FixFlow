using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FixFlow.API.OpenApi;

public static class OpenApiExtensions
{
    /// <summary>
    /// The "v1" OpenAPI document, written at build time (<c>openapi.json</c>) for the frontend client.
    /// Enums are string schemas with their names; NodaTime types are inlined as formatted strings. Operations that need
    /// an organization context also declare its 400 and 404.
    /// </summary>
    public static IServiceCollection AddFixFlowOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo { Title = "FixFlow API", Version = "v1" };
                return Task.CompletedTask;
            });
            options.AddSchemaTransformer<NodaTimeSchemaTransformer>();
            options.AddSchemaTransformer<StringEnumSchemaTransformer>();
            options.AddOperationTransformer<OrganizationContextResponsesTransformer>();
            options.CreateSchemaReferenceId = typeInfo => NodaTimeSchemaTransformer.Handles(typeInfo.Type)
                ? null
                : OpenApiOptions.CreateDefaultSchemaReferenceId(typeInfo);
        });
}
