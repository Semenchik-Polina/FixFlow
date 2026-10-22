namespace FixFlow.Application.Paging;

/// <summary>
/// A list query: <c>?page=1&amp;pageSize=25</c> (ARCHITECTURE §15). Its validator derives from
/// <see cref="PagedQueryValidator{T}"/>, which keeps <see cref="PageSize"/> within <see cref="PageSizes.Max"/>.
/// </summary>
public interface IPagedQuery
{
    /// <summary>1-based.</summary>
    int Page { get; }

    int PageSize { get; }
}

public static class PageSizes
{
    public const int Default = 25;

    public const int Max = 100;
}
