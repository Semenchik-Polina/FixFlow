using FluentValidation;

namespace FixFlow.Application.Paging;

/// <summary>The base validator for every list query: page ≥ 1 and 1 ≤ pageSize ≤ <see cref="PageSizes.Max"/>.</summary>
public abstract class PagedQueryValidator<T> : AbstractValidator<T>
    where T : IPagedQuery
{
    protected PagedQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PageSizes.Max);
    }
}
