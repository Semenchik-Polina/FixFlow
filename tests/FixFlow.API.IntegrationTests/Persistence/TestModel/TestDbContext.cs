using FixFlow.Application.Auth;
using FixFlow.Infrastructure.Persistence;
using FixFlow.Infrastructure.Persistence.Events;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.API.IntegrationTests.Persistence.TestModel;

/// <summary>The app's model and conventions plus test-only entities, in a <c>tests</c> schema.</summary>
public sealed class TestDbContext(
    DbContextOptions<TestDbContext> options, ICurrentUser currentUser, DomainEventDispatcher domainEvents)
    : FixFlowDbContext(options, currentUser, domainEvents)
{
    public const string Schema = "tests";

    public DbSet<Widget> Widgets => Set<Widget>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Widget>(widget =>
        {
            widget.ToTable("widgets", Schema);
            widget.Property(w => w.Name).HasMaxLength(100);
            widget.HasIndex(w => new { w.OrganizationId, w.Name });
        });

        // Last, so the conventions cover the test entities too
        base.OnModelCreating(builder);
    }
}
