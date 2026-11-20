using FixFlow.Application.Auth;
using FixFlow.Application.Persistence;
using FixFlow.Application.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace FixFlow.Application.Properties.ArchiveProperty;

/// <summary>
/// Archives the property and its units in one save, at one instant, so no unit stays active under an archived
/// property (plan P38). The property's archive operation covers its units.
/// </summary>
internal sealed partial class ArchivePropertyHandler(
    IFixFlowDbContext db,
    ICurrentUser currentUser,
    IResourceAuthorizer authorizer,
    IClock clock,
    ILogger<ArchivePropertyHandler> logger) : IRequestHandler<ArchivePropertyCommand, Result>
{
    public async Task<Result> Handle(ArchivePropertyCommand command, CancellationToken cancellationToken)
    {
        var property = await db.Properties
            .VisibleTo(currentUser)
            .SingleOrDefaultAsync(property => property.Id == command.Id, cancellationToken);
        if (property is null || !await authorizer.IsAllowedAsync(property, PropertyOps.Archive, cancellationToken))
        {
            return Result.NotFound();
        }

        var units = await db.Units
            .VisibleTo(currentUser)
            .Where(unit => unit.PropertyId == property.Id)
            .ToListAsync(cancellationToken);

        var now = clock.GetCurrentInstant();
        property.Archive(now);
        foreach (var unit in units)
        {
            unit.Archive(now);
        }

        await db.SaveChangesAsync(cancellationToken);

        LogArchived(currentUser.UserId, property.Id, units.Count);
        return Result.Success();
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "User {UserId} archived property {PropertyId} and its {UnitCount} units")]
    private partial void LogArchived(Guid? userId, Guid propertyId, int unitCount);
}
