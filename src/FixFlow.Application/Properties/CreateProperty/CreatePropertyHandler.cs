using FixFlow.Application.Auth;
using FixFlow.Application.Persistence;
using FixFlow.Application.Results;
using FixFlow.Domain.Properties;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FixFlow.Application.Properties.CreateProperty;

/// <summary>There is no record to check yet: the endpoint's role policy is the check.</summary>
internal sealed partial class CreatePropertyHandler(
    IFixFlowDbContext db,
    ICurrentUser currentUser,
    ILogger<CreatePropertyHandler> logger) : IRequestHandler<CreatePropertyCommand, Result<PropertyResponse>>
{
    public async Task<Result<PropertyResponse>> Handle(CreatePropertyCommand command, CancellationToken cancellationToken)
    {
        var property = Property.Create(command.Name, command.Address.ToDomain(), command.TimeZone);

        db.Properties.Add(property);
        await db.SaveChangesAsync(cancellationToken);

        LogCreated(currentUser.UserId, property.Id);
        return PropertyResponse.From(property);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} created property {PropertyId}")]
    private partial void LogCreated(Guid? userId, Guid propertyId);
}
