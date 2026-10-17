using System.Text.RegularExpressions;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.API.IntegrationTests.Persistence.TestModel;
using FixFlow.Application.Persistence;
using FixFlow.Domain;
using FixFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace FixFlow.API.IntegrationTests.Persistence;

/// <summary>
/// Guards every entity, present and future: run against the app's model (real entities from T1.2 on) and the test
/// model (which has an entity for every convention, so the checks are never vacuous).
/// </summary>
public partial class ModelConventionTests(FixFlowApiFactory api)
{
    public static TheoryData<string> Models => [nameof(FixFlowDbContext), nameof(TestDbContext)];

    [Fact]
    public async Task TestModel_HasAnEntityForEveryConvention()
    {
        var entityTypes = (await ModelAsync(nameof(TestDbContext))).GetEntityTypes().ToList();

        Assert.Contains(entityTypes, t => typeof(IOrganizationOwned).IsAssignableFrom(t.ClrType));
        Assert.Contains(entityTypes, t => typeof(ISoftDeletable).IsAssignableFrom(t.ClrType));
        Assert.Contains(entityTypes, t => typeof(IHasRowVersion).IsAssignableFrom(t.ClrType));
        Assert.Contains(entityTypes, t => t.GetProperties().Any(p => p.ClrType == typeof(Instant)));
        Assert.Contains(entityTypes, t => t.GetIndexes().Any());
    }

    [Theory]
    [MemberData(nameof(Models))]
    public async Task OrganizationOwnedEntities_HaveOrganizationFilterAndRequiredColumn(string model)
    {
        var violations = (await ModelAsync(model))
            .GetEntityTypes()
            .Where(t => typeof(IOrganizationOwned).IsAssignableFrom(t.ClrType))
            .Where(t =>
            {
                var organizationId = t.FindProperty(nameof(IOrganizationOwned.OrganizationId));
                return t.GetRootType().FindDeclaredQueryFilter(QueryFilters.Organization) is null
                    || organizationId is null
                    || organizationId.IsNullable
                    || organizationId.GetColumnName() != "organization_id";
            })
            .Select(t => t.DisplayName());

        Assert.Empty(violations);
    }

    [Theory]
    [MemberData(nameof(Models))]
    public async Task SoftDeletableEntities_HaveSoftDeleteFilter(string model)
    {
        var violations = (await ModelAsync(model))
            .GetEntityTypes()
            .Where(t => typeof(ISoftDeletable).IsAssignableFrom(t.ClrType))
            .Where(t => t.GetRootType().FindDeclaredQueryFilter(QueryFilters.SoftDelete) is null)
            .Select(t => t.DisplayName());

        Assert.Empty(violations);
    }

    [Theory]
    [MemberData(nameof(Models))]
    public async Task DatabaseNames_AreSnakeCase(string model)
    {
        var names = (await ModelAsync(model))
            .GetRelationalModel()
            .Tables
            .SelectMany(table => new[] { table.Name, table.PrimaryKey?.Name }
                .Concat(table.Columns.Select(c => c.Name))
                .Concat(table.ForeignKeyConstraints.Select(fk => fk.Name))
                .Concat(table.Indexes.Select(i => i.Name))
                .Concat(table.UniqueConstraints.Select(u => u.Name)))
            .OfType<string>();

        Assert.DoesNotContain(names, name => !SnakeCase().IsMatch(name));
    }

    [Theory]
    [MemberData(nameof(Models))]
    public async Task Tables_AreInModuleSchemas(string model)
    {
        var violations = (await ModelAsync(model))
            .GetRelationalModel()
            .Tables
            .Where(table => string.IsNullOrEmpty(table.Schema) || table.Schema == "public")
            .Select(table => table.Name);

        Assert.Empty(violations);
    }

    [Theory]
    [MemberData(nameof(Models))]
    public async Task Instants_MapToTimestamptz(string model)
    {
        var violations = (await ModelAsync(model))
            .GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(Instant) || p.ClrType == typeof(Instant?))
            .Where(p => p.GetRelationalTypeMapping().StoreType != "timestamp with time zone")
            .Select(p => $"{p.DeclaringType.DisplayName()}.{p.Name}");

        Assert.Empty(violations);
    }

    [Theory]
    [MemberData(nameof(Models))]
    public async Task RowVersions_MapToXmin(string model)
    {
        var violations = (await ModelAsync(model))
            .GetEntityTypes()
            .Where(t => typeof(IHasRowVersion).IsAssignableFrom(t.ClrType))
            .Select(t => t.FindProperty(nameof(IHasRowVersion.RowVersion))!)
            .Where(p => !p.IsConcurrencyToken
                || p.ValueGenerated != ValueGenerated.OnAddOrUpdate
                || p.GetColumnName() != "xmin"
                || p.GetRelationalTypeMapping().StoreType != "xid")
            .Select(p => p.DeclaringType.DisplayName());

        Assert.Empty(violations);
    }

    [Theory]
    [MemberData(nameof(Models))]
    public async Task EntityIds_AreNotGeneratedByTheDatabase(string model)
    {
        var violations = (await ModelAsync(model))
            .GetEntityTypes()
            .Where(t => typeof(Entity).IsAssignableFrom(t.ClrType) && t.BaseType is null)
            .Where(t => t.FindPrimaryKey()?.Properties is not [{ Name: nameof(Entity.Id), ValueGenerated: ValueGenerated.Never }])
            .Select(t => t.DisplayName());

        Assert.Empty(violations);
    }

    /// <summary>The design-time model: it keeps every annotation, unlike the runtime one.</summary>
    private async Task<IModel> ModelAsync(string model)
    {
        if (model == nameof(TestDbContext))
        {
            await using var scope = api.Persistence.CreateScope(TestCurrentUser.WithoutOrganization());
            return scope.Db.GetService<IDesignTimeModel>().Model;
        }

        await using var appScope = api.Services.CreateAsyncScope();
        return appScope.ServiceProvider.GetRequiredService<FixFlowDbContext>().GetService<IDesignTimeModel>().Model;
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex SnakeCase();
}
