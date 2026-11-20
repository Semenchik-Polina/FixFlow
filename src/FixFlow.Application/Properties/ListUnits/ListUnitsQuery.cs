using FixFlow.Application.Paging;
using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Properties.ListUnits;

/// <summary>A property's units, ordered by name, which is unique in the property.</summary>
public sealed record ListUnitsQuery(Guid PropertyId, int Page, int PageSize)
    : IRequest<Result<PagedResult<UnitResponse>>>, IPagedQuery;
