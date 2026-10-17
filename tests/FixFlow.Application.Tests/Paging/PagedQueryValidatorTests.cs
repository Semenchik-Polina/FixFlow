using FixFlow.Application.Paging;

namespace FixFlow.Application.Tests.Paging;

public class PagedQueryValidatorTests
{
    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(1, PageSizes.Max, true)]
    [InlineData(1, PageSizes.Max + 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(0, PageSizes.Default, false)]
    public void Validate_PageAndPageSize_EnforcesBounds(int page, int pageSize, bool valid)
    {
        var result = new QueryValidator().Validate(new Query(page, pageSize));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void PagedQueries_InApplication_HaveValidatorDerivingFromPagedQueryValidator()
    {
        var types = ApplicationAssembly.Instance.GetTypes();
        var validated = types.Select(PagedQueryOf).OfType<Type>().ToHashSet();

        var unvalidated = types
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IPagedQuery)))
            .Where(type => !validated.Contains(type))
            .Select(type => type.FullName)
            .ToList();

        Assert.Empty(unvalidated);
    }

    /// <summary>The query a <see cref="PagedQueryValidator{T}"/> subclass validates, or null.</summary>
    private static Type? PagedQueryOf(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(PagedQueryValidator<>))
            {
                return current.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private sealed record Query(int Page, int PageSize) : IPagedQuery;

    private sealed class QueryValidator : PagedQueryValidator<Query>;
}
