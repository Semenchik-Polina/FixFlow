using System.Linq.Expressions;
using System.Reflection;
using FixFlow.Application.Auth;
using FixFlow.Application.Errors;
using FixFlow.Application.Persistence;
using FixFlow.Domain;
using FixFlow.Infrastructure.Persistence.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace FixFlow.Infrastructure.Persistence;

/// <summary>
/// The one DbContext (§10). Every entity gets the conventions in <see cref="OnModelCreating"/> from its marker
/// interfaces, so configurations only map their own columns. Saving dispatches domain events in two phases
/// (ADR 0005); the interceptors stamp the organization and the audit fields.
/// </summary>
public class FixFlowDbContext : DbContext, IFixFlowDbContext
{
    /// <summary>In-transaction handlers may raise further events; more rounds than this means a cycle.</summary>
    private const int MaxInTransactionRounds = 10;

    private static readonly PropertyInfo _currentOrganizationIdProperty =
        typeof(FixFlowDbContext).GetProperty(nameof(CurrentOrganizationId), BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly ICurrentUser _currentUser;
    private readonly DomainEventDispatcher _domainEvents;

    /// <summary>Saved inside an explicit transaction, waiting for its commit.</summary>
    private readonly List<IDomainEvent> _pendingPostCommit = [];

    public FixFlowDbContext(
        DbContextOptions<FixFlowDbContext> options, ICurrentUser currentUser, DomainEventDispatcher domainEvents)
        : this((DbContextOptions)options, currentUser, domainEvents)
    {
    }

    /// <summary>For contexts that add test-only entities.</summary>
    protected FixFlowDbContext(DbContextOptions options, ICurrentUser currentUser, DomainEventDispatcher domainEvents)
        : base(options)
    {
        _currentUser = currentUser;
        _domainEvents = domainEvents;
    }

    /// <summary>Read by the organization filter on every query; EF Core turns it into a parameter.</summary>
    private Guid? CurrentOrganizationId => _currentUser.OrganizationId;

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var domainEvents = await DispatchInTransactionAsync(cancellationToken);

        int written;
        try
        {
            written = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException("The record was changed by someone else.", exception);
        }

        if (Database.CurrentTransaction is not null)
        {
            // DomainEventsTransactionInterceptor dispatches them once the transaction commits
            _pendingPostCommit.AddRange(domainEvents);
            return written;
        }

        // Events still pending were saved in an explicit transaction disposed without commit or rollback, so rolled
        // back. The interceptor drops them when the next transaction starts, but a save may start none.
        _pendingPostCommit.Clear();
        _pendingPostCommit.AddRange(domainEvents);
        await DispatchPostCommitAsync();
        return written;
    }

    /// <summary>Not supported: domain event handlers are async, and skipping them would break consistency.</summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Use SaveChangesAsync: it dispatches the domain events.");

    internal bool HasPendingPostCommitEvents => _pendingPostCommit.Count > 0;

    internal async Task DispatchPostCommitAsync()
    {
        var domainEvents = _pendingPostCommit.ToList();
        _pendingPostCommit.Clear();

        foreach (var domainEvent in domainEvents)
        {
            await _domainEvents.DispatchPostCommitAsync(domainEvent);
        }
    }

    internal void DiscardPostCommitEvents() => _pendingPostCommit.Clear();

    /// <summary>
    /// Derived contexts configure their own entities first, then call this, so the conventions cover them too.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(InfrastructureAssembly.Instance);

        // Filters and keys belong to the root of a hierarchy; derived types inherit them. Shared-type entities (the
        // join tables of many-to-many relationships) are property bags with no marker interfaces, and
        // ModelBuilder.Entity(Type) can't reach them.
        var roots = modelBuilder.Model
            .GetEntityTypes()
            .Where(t => t.BaseType is null && !t.IsOwned() && !t.HasSharedClrType);
        foreach (var entityType in roots)
        {
            ApplyConventions(modelBuilder.Entity(entityType.ClrType));
        }
    }

    private void ApplyConventions(EntityTypeBuilder builder)
    {
        var clrType = builder.Metadata.ClrType;

        if (typeof(Entity).IsAssignableFrom(clrType))
        {
            // The domain assigns UUIDv7 ids. Value-generated keys would make EF treat a new child, added to a
            // tracked aggregate, as an existing row and UPDATE it.
            builder.Property(nameof(Entity.Id)).ValueGeneratedNever();
        }

        if (typeof(AggregateRoot).IsAssignableFrom(clrType))
        {
            builder.Ignore(nameof(AggregateRoot.DomainEvents));
        }

        if (typeof(IOrganizationOwned).IsAssignableFrom(clrType))
        {
            builder.HasQueryFilter(QueryFilters.Organization, OrganizationFilter(clrType));
        }

        if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
        {
            builder.HasQueryFilter(QueryFilters.SoftDelete, SoftDeleteFilter(clrType));
        }

        if (typeof(IHasRowVersion).IsAssignableFrom(clrType))
        {
            // Npgsql maps a uint row version to the xmin system column
            builder.Property(nameof(IHasRowVersion.RowVersion)).IsRowVersion();
        }
    }

    /// <summary><c>entity => entity.OrganizationId == CurrentOrganizationId</c>: no organization context, no rows.</summary>
    private LambdaExpression OrganizationFilter(Type clrType)
    {
        var entity = Expression.Parameter(clrType, "entity");
        var body = Expression.Equal(
            Expression.Convert(Expression.Property(entity, nameof(IOrganizationOwned.OrganizationId)), typeof(Guid?)),
            Expression.Property(Expression.Constant(this), _currentOrganizationIdProperty));
        return Expression.Lambda(body, entity);
    }

    /// <summary><c>entity => entity.DeletedAt == null</c>.</summary>
    private static LambdaExpression SoftDeleteFilter(Type clrType)
    {
        var entity = Expression.Parameter(clrType, "entity");
        var body = Expression.Equal(
            Expression.Property(entity, nameof(ISoftDeletable.DeletedAt)),
            Expression.Constant(null, typeof(Instant?)));
        return Expression.Lambda(body, entity);
    }

    /// <summary>
    /// Calls the in-transaction handlers of the events raised by tracked aggregates, then of the events those handlers
    /// raise, until none are left. Returns every event: each one also goes to its post-commit handlers after the
    /// commit. The phase belongs to the handler, not the event (ARCHITECTURE §12).
    /// </summary>
    private async Task<List<IDomainEvent>> DispatchInTransactionAsync(CancellationToken cancellationToken)
    {
        var dispatched = new List<IDomainEvent>();

        for (var round = 1; ; round++)
        {
            var raised = TakeDomainEvents();
            if (raised.Count == 0)
            {
                return dispatched;
            }

            if (round > MaxInTransactionRounds)
            {
                throw new InvalidOperationException(
                    $"In-transaction handlers were still raising domain events after {MaxInTransactionRounds} " +
                    $"rounds (last: {raised[0].GetType().Name}). Is there a cycle?");
            }

            foreach (var domainEvent in raised)
            {
                await _domainEvents.DispatchInTransactionAsync(domainEvent, cancellationToken);
            }

            dispatched.AddRange(raised);
        }
    }

    private List<IDomainEvent> TakeDomainEvents()
    {
        var aggregates = ChangeTracker
            .Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        var domainEvents = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToList();
        aggregates.ForEach(aggregate => aggregate.ClearDomainEvents());
        return domainEvents;
    }
}
