using FixFlow.Application.Paging;
using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Properties.ListProperties;

/// <summary>The organization's properties, ordered by name.</summary>
/// <param name="Name">Trimmed; when not blank, only the properties whose name contains it, ignoring case.</param>
public sealed record ListPropertiesQuery(int Page, int PageSize, string? Name)
    : IRequest<Result<PagedResult<PropertyResponse>>>, IPagedQuery;
