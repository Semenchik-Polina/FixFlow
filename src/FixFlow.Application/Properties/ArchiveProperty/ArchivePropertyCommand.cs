using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Properties.ArchiveProperty;

/// <summary>
/// A Manager or Admin archives a property and every unit in it (plan P38). The id comes from the route, so there is
/// no validator.
/// </summary>
public sealed record ArchivePropertyCommand(Guid Id) : IRequest<Result>;
