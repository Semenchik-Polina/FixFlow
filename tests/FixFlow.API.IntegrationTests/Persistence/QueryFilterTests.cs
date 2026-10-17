using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.IntegrationTests.Persistence.TestModel;
using FixFlow.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.API.IntegrationTests.Persistence;

public class QueryFilterTests(FixFlowApiFactory api)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Query_RowOfAnotherOrganization_IsInvisible()
    {
        var owner = TestCurrentUser.InNewOrganization();
        var widget = await SaveWidgetAsync(owner);

        Assert.True(await ExistsAsync(owner, widget.Id));
        Assert.False(await ExistsAsync(TestCurrentUser.InNewOrganization(), widget.Id));
        Assert.False(await ExistsAsync(TestCurrentUser.WithoutOrganization(), widget.Id));
    }

    [Fact]
    public async Task Query_SoftDeletedRow_IsInvisible()
    {
        var owner = TestCurrentUser.InNewOrganization();
        var widget = await SaveWidgetAsync(owner);

        await using (var scope = api.Persistence.CreateScope(owner))
        {
            var saved = await scope.Db.Widgets.SingleAsync(w => w.Id == widget.Id, _ct);
            saved.Delete(FixFlowApiFactory.SharedNow);
            await scope.Db.SaveChangesAsync(_ct);
        }

        Assert.False(await ExistsAsync(owner, widget.Id));

        await using var reader = api.Persistence.CreateScope(owner);
        // Checks that the row is still there, only filtered out
        Assert.True(await reader.Db.Widgets
            .IgnoreQueryFilters([QueryFilters.SoftDelete])
            .AnyAsync(w => w.Id == widget.Id, _ct));
    }

    private async Task<Widget> SaveWidgetAsync(TestCurrentUser user)
    {
        await using var scope = api.Persistence.CreateScope(user);
        var widget = new Widget("Boiler");
        scope.Db.Widgets.Add(widget);
        await scope.Db.SaveChangesAsync(_ct);
        return widget;
    }

    private async Task<bool> ExistsAsync(TestCurrentUser user, Guid widgetId)
    {
        await using var scope = api.Persistence.CreateScope(user);
        return await scope.Db.Widgets.AnyAsync(w => w.Id == widgetId, _ct);
    }
}
