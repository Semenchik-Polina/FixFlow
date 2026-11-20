using FixFlow.Application.Auth;
using FixFlow.Application.Persistence;
using FixFlow.Application.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FixFlow.Application.Properties.UpdateProperty;

/// <summary>Last write wins: properties have no row version (§10 keeps it to work orders and requests).</summary>
internal sealed partial class UpdatePropertyHandler(
    IFixFlowDbContext db,
    ICurrentUser currentUser,
    IResourceAuthorizer authorizer,
    ILogger<UpdatePropertyHandler> logger) : IRequestHandler<UpdatePropertyCommand, Result>
{
    public async Task<Result> Handle(UpdatePropertyCommand command, CancellationToken cancellationToken)
    {
        var property = await db.Properties
            .VisibleTo(currentUser)
            .SingleOrDefaultAsync(property => property.Id == command.Id, cancellationToken);
        if (property is null || !await authorizer.IsAllowedAsync(property, PropertyOps.Update, cancellationToken))
        {
            return Result.NotFound();
        }

        property.Update(command.Name, command.Address.ToDomain(), command.TimeZone);
        await db.SaveChangesAsync(cancellationToken);

        LogUpdated(currentUser.UserId, property.Id);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} updated property {PropertyId}")]
    private partial void LogUpdated(Guid? userId, Guid propertyId);
}
