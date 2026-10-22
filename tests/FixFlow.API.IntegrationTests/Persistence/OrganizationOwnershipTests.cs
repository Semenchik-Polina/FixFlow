using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.IntegrationTests.Persistence.TestModel;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.API.IntegrationTests.Persistence;

public class OrganizationOwnershipTests(FixFlowApiFactory api)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveChanges_NewRowWithoutOrganization_GetsTheRequestsOrganization()
    {
        var user = TestCurrentUser.InNewOrganization();
        var widget = new Widget("Boiler");

        await using (var scope = api.Persistence.CreateScope(user))
        {
            scope.Db.Widgets.Add(widget);
            await scope.Db.SaveChangesAsync(_ct);
        }

        await using var reader = api.Persistence.CreateScope(user);
        var saved = await reader.Db.Widgets.SingleAsync(w => w.Id == widget.Id, _ct);
        Assert.Equal(user.OrganizationId, saved.OrganizationId);
    }

    [Fact]
    public async Task SaveChanges_WithoutOrganizationContext_Throws()
    {
        await using var scope = api.Persistence.CreateScope(TestCurrentUser.WithoutOrganization());
        scope.Db.Widgets.Add(new Widget("Boiler"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Db.SaveChangesAsync(_ct));
    }

    [Fact]
    public async Task SaveChanges_NewRowForAnotherOrganization_Throws()
    {
        var other = TestCurrentUser.InNewOrganization();
        await using var scope = api.Persistence.CreateScope(TestCurrentUser.InNewOrganization());
        scope.Db.Widgets.Add(new Widget("Boiler", other.OrganizationId!.Value));

        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Db.SaveChangesAsync(_ct));
    }

    [Fact]
    public async Task SaveChanges_ChangedRowOfAnotherOrganization_Throws()
    {
        var widget = new Widget("Boiler");
        await using (var owner = api.Persistence.CreateScope(TestCurrentUser.InNewOrganization()))
        {
            owner.Db.Widgets.Add(widget);
            await owner.Db.SaveChangesAsync(_ct);
        }

        // The filter hides it from other organizations; attaching it directly must not get around that
        await using var scope = api.Persistence.CreateScope(TestCurrentUser.InNewOrganization());
        scope.Db.Widgets.Attach(widget);
        widget.Rename("Taken over");

        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Db.SaveChangesAsync(_ct));
    }
}
