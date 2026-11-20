using FixFlow.Application.Auth;
using FixFlow.Application.Errors;
using FixFlow.Application.Persistence;
using FixFlow.Application.Properties.CreateUnit;
using FixFlow.Application.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FixFlow.Application.Properties.RenameUnit;

internal sealed partial class RenameUnitHandler(
    IFixFlowDbContext db,
    ICurrentUser currentUser,
    IResourceAuthorizer authorizer,
    ILogger<RenameUnitHandler> logger) : IRequestHandler<RenameUnitCommand, Result>
{
    public async Task<Result> Handle(RenameUnitCommand command, CancellationToken cancellationToken)
    {
        var unit = await db.FindUnitAsync(currentUser, command.PropertyId, command.UnitId, cancellationToken);
        if (unit is null || !await authorizer.IsAllowedAsync(unit, UnitOps.Rename, cancellationToken))
        {
            return Result.NotFound();
        }

        unit.Rename(command.Name);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        // The save failed, so the unit of work is over: nothing more is saved
        catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == UniqueIndexes.UnitName)
        {
            return CreateUnitHandler.NameTaken();
        }

        LogRenamed(currentUser.UserId, unit.Id);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} renamed unit {UnitId}")]
    private partial void LogRenamed(Guid? userId, Guid unitId);
}
