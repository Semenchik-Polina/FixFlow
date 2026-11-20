using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Properties.GetProperty;

/// <summary>One of the organization's properties. The id comes from the route, so there is no validator.</summary>
public sealed record GetPropertyQuery(Guid Id) : IRequest<Result<PropertyResponse>>;
