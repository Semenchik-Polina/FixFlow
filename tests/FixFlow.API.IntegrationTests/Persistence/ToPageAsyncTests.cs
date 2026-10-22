using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.IntegrationTests.Persistence.TestModel;
using FixFlow.Application.Paging;

namespace FixFlow.API.IntegrationTests.Persistence;

public class ToPageAsyncTests(FixFlowApiFactory api)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task ToPageAsync_LastPage_ReturnsRemainingItemsAndTotal()
    {
        var user = TestCurrentUser.InNewOrganization();
        await using (var scope = api.Persistence.CreateScope(user))
        {
            scope.Db.Widgets.AddRange(Enumerable.Range(0, 30).Select(i => new Widget($"Widget {i:D2}")));
            await scope.Db.SaveChangesAsync(_ct);
        }

        await using var reader = api.Persistence.CreateScope(user);
        var page = await reader.Db.Widgets
            .OrderBy(w => w.Name)
            .Select(w => w.Name)
            .ToPageAsync(new PageRequest(Page: 2, PageSize: 25), _ct);

        Assert.Equal(["Widget 25", "Widget 26", "Widget 27", "Widget 28", "Widget 29"], page.Items);
        Assert.Equal(2, page.Page);
        Assert.Equal(25, page.PageSize);
        Assert.Equal(30, page.TotalCount);
    }

    [Fact]
    public async Task ToPageAsync_PageBeyondInt32Offset_ReturnsEmptyItemsAndTotal()
    {
        var user = TestCurrentUser.InNewOrganization();
        await using (var scope = api.Persistence.CreateScope(user))
        {
            scope.Db.Widgets.AddRange(Enumerable.Range(0, 3).Select(i => new Widget($"Widget {i:D2}")));
            await scope.Db.SaveChangesAsync(_ct);
        }

        await using var reader = api.Persistence.CreateScope(user);
        var page = await reader.Db.Widgets
            .OrderBy(w => w.Name)
            .Select(w => w.Name)
            .ToPageAsync(new PageRequest(Page: int.MaxValue, PageSize: PageSizes.Max), _ct);

        Assert.Empty(page.Items);
        Assert.Equal(int.MaxValue, page.Page);
        Assert.Equal(PageSizes.Max, page.PageSize);
        Assert.Equal(3, page.TotalCount);
    }

    private sealed record PageRequest(int Page, int PageSize) : IPagedQuery;
}
