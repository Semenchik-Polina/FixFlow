namespace FixFlow.Application.Paging;

/// <summary>One page of a list, plus the total across all pages.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
