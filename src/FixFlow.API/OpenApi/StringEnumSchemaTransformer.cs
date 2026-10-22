using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FixFlow.API.OpenApi;

/// <summary>
/// Enums are serialized as their names (<see cref="Json.JsonExtensions"/>). The generated schema lists the names but
/// leaves out the type; this states <c>type: string</c> so the client and readers don't have to infer it.
/// </summary>
internal sealed class StringEnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if ((Nullable.GetUnderlyingType(type) ?? type).IsEnum && schema.Enum is { Count: > 0 } && schema.Type is null)
        {
            schema.Type = JsonSchemaType.String;
        }

        return Task.CompletedTask;
    }
}
