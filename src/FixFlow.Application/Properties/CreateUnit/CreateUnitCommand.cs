using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Properties.CreateUnit;

/// <summary>A Manager or Admin adds a unit to a property (from a <see cref="UnitRequest"/>, with the property's id from the route).</summary>
public sealed record CreateUnitCommand(Guid PropertyId, string Name) : IRequest<Result<UnitResponse>>;
