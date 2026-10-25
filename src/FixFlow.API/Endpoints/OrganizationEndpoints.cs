using FixFlow.Application.Organizations.CreateOrganization;
using MediatR;

namespace FixFlow.API.Endpoints;

/// <summary>
/// Organizations themselves. Creating one is organization-less: the organization context middleware (T1.5) leaves it
/// alone, and the handler opens a trusted scope for the new organization.
/// </summary>
internal sealed class OrganizationEndpoints : IEndpointModule
{
    public string Prefix => "/organizations";

    public void Map(RouteGroupBuilder group)
    {
        // "", not "/": the path stays /api/v1/organizations, without a trailing slash
        group.MapPost("", async (CreateOrganizationCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToCreated())
            .RequireAuthorization()
            .WithName("CreateOrganization")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            // A taken slug is a field error
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
    }
}
