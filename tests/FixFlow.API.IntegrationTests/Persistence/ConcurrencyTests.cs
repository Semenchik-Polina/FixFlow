using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.IntegrationTests.Persistence.TestModel;
using FixFlow.Application.Errors;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.API.IntegrationTests.Persistence;

public class ConcurrencyTests(FixFlowApiFactory api)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveChanges_RowChangedSinceRead_ThrowsConcurrencyConflict()
    {
        var user = TestCurrentUser.InNewOrganization();
        var widget = new Widget("Boiler");
        await using (var scope = api.Persistence.CreateScope(user))
        {
            scope.Db.Widgets.Add(widget);
            await scope.Db.SaveChangesAsync(_ct);
        }

        await using var first = api.Persistence.CreateScope(user);
        await using var second = api.Persistence.CreateScope(user.Colleague());
        var firstCopy = await first.Db.Widgets.SingleAsync(w => w.Id == widget.Id, _ct);
        var secondCopy = await second.Db.Widgets.SingleAsync(w => w.Id == widget.Id, _ct);

        firstCopy.Rename("Water heater");
        await first.Db.SaveChangesAsync(_ct);
        secondCopy.Rename("Furnace");

        var conflict = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => second.Db.SaveChangesAsync(_ct));
        Assert.IsType<DbUpdateConcurrencyException>(conflict.InnerException);
    }
}
