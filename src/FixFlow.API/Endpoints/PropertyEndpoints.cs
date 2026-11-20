using FixFlow.API.Auth;
using FixFlow.Application.Paging;
using FixFlow.Application.Properties;
using FixFlow.Application.Properties.ArchiveProperty;
using FixFlow.Application.Properties.ArchiveUnit;
using FixFlow.Application.Properties.CreateProperty;
using FixFlow.Application.Properties.CreateUnit;
using FixFlow.Application.Properties.GetProperty;
using FixFlow.Application.Properties.ListProperties;
using FixFlow.Application.Properties.ListUnits;
using FixFlow.Application.Properties.RenameUnit;
using FixFlow.Application.Properties.UpdateProperty;
using MediatR;

namespace FixFlow.API.Endpoints;

/// <summary>
/// The organization's properties and their units, for Managers and Admins (<see cref="Policies.CanManageProperties"/>).
/// Ids come from the route only; bodies carry the fields. The organization context's 400 and 404 and the policy's 403
/// are declared by the OpenAPI transformers.
/// </summary>
internal sealed class PropertyEndpoints : IEndpointModule
{
    private const string GetPropertyRoute = "GetProperty";

    public string Prefix => "/properties";

    public void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization(Policies.CanManageProperties);

        // "", not "/": the path stays /api/v1/properties, without a trailing slash
        group.MapGet("", async (
                    ISender sender,
                    CancellationToken ct,
                    int page = 1,
                    int pageSize = PageSizes.Default,
                    string? name = null) =>
                (await sender.Send(new ListPropertiesQuery(page, pageSize, name), ct)).ToOk())
            .WithName("ListProperties")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetPropertyQuery(id), ct)).ToOk())
            .WithName(GetPropertyRoute)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("", async (PropertyRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreatePropertyCommand(request.Name, request.Address, request.TimeZone), ct))
                .ToCreatedAtRoute(GetPropertyRoute, property => new { id = property.Id }))
            .WithName("CreateProperty")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPut("/{id:guid}", async (Guid id, PropertyRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdatePropertyCommand(id, request.Name, request.Address, request.TimeZone), ct))
                .ToNoContent())
            .WithName("UpdateProperty")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // An action, not DELETE: the property and its units are archived, not deleted (plan P38)
        group.MapPost("/{id:guid}/archive", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ArchivePropertyCommand(id), ct)).ToNoContent())
            .WithName("ArchiveProperty")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/{propertyId:guid}/units", async (
                    Guid propertyId,
                    ISender sender,
                    CancellationToken ct,
                    int page = 1,
                    int pageSize = PageSizes.Default) =>
                (await sender.Send(new ListUnitsQuery(propertyId, page, pageSize), ct)).ToOk())
            .WithName("ListUnits")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Units have no GET endpoint of their own, so no Location
        group.MapPost("/{propertyId:guid}/units", async (
                    Guid propertyId, UnitRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateUnitCommand(propertyId, request.Name), ct)).ToCreated())
            .WithName("CreateUnit")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            // A name another unit of the property has is a field error
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("/{propertyId:guid}/units/{unitId:guid}", async (
                    Guid propertyId, Guid unitId, UnitRequest request, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RenameUnitCommand(propertyId, unitId, request.Name), ct)).ToNoContent())
            .WithName("RenameUnit")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            // A name another unit of the property has is a field error
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{propertyId:guid}/units/{unitId:guid}/archive", async (
                    Guid propertyId, Guid unitId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ArchiveUnitCommand(propertyId, unitId), ct)).ToNoContent())
            .WithName("ArchiveUnit")
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
