using FixFlow.Application.Auth;
using FixFlow.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NodaTime;

namespace FixFlow.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps <see cref="IAuditable"/> rows (§10): created on insert, updated on every later change. The creation stamp
/// is never rewritten.
/// </summary>
internal sealed class AuditInterceptor(ICurrentUser currentUser, IClock clock) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context!);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext context)
    {
        var now = clock.GetCurrentInstant();

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(nameof(IAuditable.CreatedAt)).CurrentValue = now;
                    entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue = currentUser.UserId;
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(IAuditable.UpdatedAt)).CurrentValue = now;
                    entry.Property(nameof(IAuditable.UpdatedBy)).CurrentValue = currentUser.UserId;
                    // Reverts any change to the original values
                    entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                    break;
            }
        }
    }
}
