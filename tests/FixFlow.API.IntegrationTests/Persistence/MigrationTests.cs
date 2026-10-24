using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.API.IntegrationTests.Persistence;

public class MigrationTests(FixFlowApiFactory api)
{
    [Fact]
    public async Task Migrations_CoverTheModel()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FixFlowDbContext>();

        // Fails for a model change without a migration: add one with dotnet ef migrations add (CLAUDE.md §2)
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
