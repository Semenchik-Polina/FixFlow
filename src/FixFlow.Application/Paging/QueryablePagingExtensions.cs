using Microsoft.EntityFrameworkCore;

namespace FixFlow.Application.Paging;

public static class QueryablePagingExtensions
{
    /// <summary>
    /// Runs the query for one page, plus a count of all matching rows. Order the query first: without an
    /// <c>OrderBy</c> the rows of a page aren't deterministic.
    /// </summary>
    public static async Task<PagedResult<T>> ToPageAsync<T>(
        this IQueryable<T> query, IPagedQuery page, CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page.Page - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, page.Page, page.PageSize, totalCount);
    }
}
