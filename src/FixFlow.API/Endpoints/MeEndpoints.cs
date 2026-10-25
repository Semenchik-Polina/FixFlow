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
    }
}
