using FixFlow.Application.Auth;
using FixFlow.Application.Errors;
using FixFlow.Application.Persistence;
using FixFlow.Application.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unit = FixFlow.Domain.Properties.Unit;

namespace FixFlow.Application.Properties.CreateUnit;

internal sealed partial class CreateUnitHandler(
    IFixFlowDbContext db,
    ICurrentUser currentUser,
    IResourceAuthorizer authorizer,
    ILogger<CreateUnitHandler> logger) : IRequestHandler<CreateUnitCommand, Result<UnitResponse>>
{
    public async Task<Result<UnitResponse>> Handle(CreateUnitCommand command, CancellationToken cancellationToken)
    {
        var property = await db.Properties
            .VisibleTo(currentUser)
            .SingleOrDefaultAsync(property => property.Id == command.PropertyId, cancellationToken);
        if (property is null || !await authorizer.IsAllowedAsync(property, PropertyOps.AddUnit, cancellationToken))
        {
            return Result.NotFound();
        }

        var unit = Unit.Create(property.Id, command.Name);
        db.Units.Add(unit);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        // The save failed, so the unit of work is over: nothing more is saved
        catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == UniqueIndexes.UnitName)
        {
            return NameTaken();
        }

        LogCreated(currentUser.UserId, unit.Id, property.Id);
        return UnitResponse.From(unit);
    }

    /// <summary>The field error for a name that another unit of the property has, ignoring case.</summary>
    public static ResultError NameTaken() =>
        Result.FieldError(nameof(CreateUnitCommand.Name), "Another unit in this property already has this name.");

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} created unit {UnitId} in property {PropertyId}")]
    private partial void LogCreated(Guid? userId, Guid unitId, Guid propertyId);
}
