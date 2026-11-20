using FixFlow.Application.Auth;
using FixFlow.Application.Persistence;
using FixFlow.Application.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace FixFlow.Application.Properties.ArchiveUnit;

internal sealed partial class ArchiveUnitHandler(
    IFixFlowDbContext db,
    ICurrentUser currentUser,
    IResourceAuthorizer authorizer,
    IClock clock,
    ILogger<ArchiveUnitHandler> logger) : IRequestHandler<ArchiveUnitCommand, Result>
{
    public async Task<Result> Handle(ArchiveUnitCommand command, CancellationToken cancellationToken)
    {
        var unit = await db.FindUnitAsync(currentUser, command.PropertyId, command.UnitId, cancellationToken);
        if (unit is null || !await authorizer.IsAllowedAsync(unit, UnitOps.Archive, cancellationToken))
        {
            return Result.NotFound();
        }

        unit.Archive(clock.GetCurrentInstant());
        await db.SaveChangesAsync(cancellationToken);

        LogArchived(currentUser.UserId, unit.Id);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} archived unit {UnitId}")]
    private partial void LogArchived(Guid? userId, Guid unitId);
}
