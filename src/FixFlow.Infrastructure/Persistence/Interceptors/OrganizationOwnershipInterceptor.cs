using FixFlow.Application.Auth;
using FixFlow.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FixFlow.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Organization-owned rows are written only for the request's organization (CLAUDE.md §3.1, S9). A new row without an
/// organization gets the current one. Writing without an organization context, for another organization, or moving
/// a row to another organization throws: that is a bug, never a user error.
/// </summary>
internal sealed class OrganizationOwnershipInterceptor(ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context!);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries<IOrganizationOwned>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var entityName = entry.Metadata.DisplayName();
            var organizationId = currentUser.OrganizationId
                ?? throw new InvalidOperationException($"Saving a {entityName} needs an organization context.");

            var property = entry.Property(nameof(IOrganizationOwned.OrganizationId));
            if (entry.State == EntityState.Added && entry.Entity.OrganizationId == Guid.Empty)
            {
                property.CurrentValue = organizationId;
            }

            var belongsToCurrent = entry.Entity.OrganizationId == organizationId
                && (entry.State == EntityState.Added || (Guid)property.OriginalValue! == organizationId);
            if (!belongsToCurrent)
            {
                throw new InvalidOperationException(
                    $"A {entityName} can only be saved for the organization of the request.");
            }
        }
    }
}
