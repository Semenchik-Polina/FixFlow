using FixFlow.API.RateLimiting;
using FixFlow.Application.Identity.ChangeName;
using FixFlow.Application.Identity.ChangePassword;
using FixFlow.Application.Identity.GetMe;
using MediatR;

namespace FixFlow.API.Endpoints;

/// <summary>
/// The caller's own account. Organization-less: the organization context middleware (T1.5) leaves it alone.
/// </summary>
internal sealed class MeEndpoints : IEndpointModule
{
    public string Prefix => "/me";

    public void Map(RouteGroupBuilder group)
    {
        // "", not "/": the path stays /api/v1/me, without a trailing slash
        group.MapGet("", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetMeQuery(), ct)).ToOk())
            .RequireAuthorization()
            .WithName("GetMe")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Takes a password, so it's rate-limited like login (S6, plan P28)
        group.MapPost("/password", async (ChangePasswordCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToNoContent())
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithName("ChangePassword")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            // A wrong current password is a field error
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPut("/name", async (ChangeNameCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToNoContent())
            .RequireAuthorization()
            .WithName("ChangeName")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
