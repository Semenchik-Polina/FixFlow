using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Properties.RenameUnit;

/// <summary>A Manager or Admin renames a unit (from a <see cref="UnitRequest"/>, with both ids from the route).</summary>
public sealed record RenameUnitCommand(Guid PropertyId, Guid UnitId, string Name) : IRequest<Result>;
