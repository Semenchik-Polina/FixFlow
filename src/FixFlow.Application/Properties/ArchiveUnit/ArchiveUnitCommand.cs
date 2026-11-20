using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Properties.ArchiveUnit;

/// <summary>
/// A Manager or Admin archives a unit; its name is free again. Both ids come from the route, so there is no validator.
/// </summary>
public sealed record ArchiveUnitCommand(Guid PropertyId, Guid UnitId) : IRequest<Result>;
