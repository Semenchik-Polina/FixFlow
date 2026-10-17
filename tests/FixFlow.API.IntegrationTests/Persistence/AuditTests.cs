using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.IntegrationTests.Persistence.TestModel;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Testing;

namespace FixFlow.API.IntegrationTests.Persistence;

public class AuditTests(FixFlowApiFactory api)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveChanges_NewRow_StampsCreated()
    {
        var user = TestCurrentUser.InNewOrganization();
        var widget = new Widget("Boiler");

        await using (var scope = api.Persistence.CreateScope(user))
        {
            scope.Db.Widgets.Add(widget);
            await scope.Db.SaveChangesAsync(_ct);
        }

        var saved = await LoadAsync(user, widget.Id);
        Assert.Equal(FixFlowApiFactory.SharedNow, saved.CreatedAt);
        Assert.Equal(user.UserId, saved.CreatedBy);
        Assert.Null(saved.UpdatedAt);
        Assert.Null(saved.UpdatedBy);
    }

    [Fact]
    public async Task SaveChanges_ChangedRow_StampsUpdatedAndKeepsCreated()
    {
        var creator = TestCurrentUser.InNewOrganization();
        var editor = creator.Colleague();
        var later = FixFlowApiFactory.SharedNow + Duration.FromHours(3);
        var widget = new Widget("Boiler");

        await using (var scope = api.Persistence.CreateScope(creator))
        {
            scope.Db.Widgets.Add(widget);
            await scope.Db.SaveChangesAsync(_ct);
        }

        await using (var scope = api.Persistence.CreateScope(editor, new FakeClock(later)))
        {
            var saved = await scope.Db.Widgets.SingleAsync(w => w.Id == widget.Id, _ct);
            saved.Rename("Water heater");
            await scope.Db.SaveChangesAsync(_ct);
        }

        var updated = await LoadAsync(creator, widget.Id);
        Assert.Equal(later, updated.UpdatedAt);
        Assert.Equal(editor.UserId, updated.UpdatedBy);
        Assert.Equal(FixFlowApiFactory.SharedNow, updated.CreatedAt);
        Assert.Equal(creator.UserId, updated.CreatedBy);
    }

    private async Task<Widget> LoadAsync(TestCurrentUser user, Guid widgetId)
    {
        await using var scope = api.Persistence.CreateScope(user);
        return await scope.Db.Widgets.AsNoTracking().SingleAsync(w => w.Id == widgetId, _ct);
    }
}
