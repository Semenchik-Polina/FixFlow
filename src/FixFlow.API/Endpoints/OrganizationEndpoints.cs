using FixFlow.API.Organizations;
using FixFlow.Application.Organizations.CreateOrganization;
using MediatR;

namespace FixFlow.API.Endpoints;

/// <summary>
/// Organizations themselves. Organization-less
/// (<see cref="OrganizationContextExtensions.WithoutOrganization{TBuilder}"/>): creating one ignores the
/// <c>X-Organization</c> header, and the handler opens a trusted scope for the new organization. An endpoint for the
/// request's own organization added here marks itself <see cref="OrganizationContextExtensions.RequireOrganization{TBuilder}"/>.
/// </summary>
internal sealed class OrganizationEndpoints : IEndpointModule
{
    public string Prefix => "/organizations";

    public void Map(RouteGroupBuilder group)
    {
        group.WithoutOrganization();

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
