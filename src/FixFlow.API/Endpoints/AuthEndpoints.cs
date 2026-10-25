using FixFlow.API.RateLimiting;
using FixFlow.Application.Identity.LogIn;
using FixFlow.Application.Identity.LogOut;
using FixFlow.Application.Identity.SignUp;
using MediatR;

namespace FixFlow.API.Endpoints;

/// <summary>
/// Signup, login and logout (plan P26). Organization-less: the organization context middleware (T1.5) leaves them
/// alone, and signup opens a trusted scope of its own.
/// </summary>
internal sealed class AuthEndpoints : IEndpointModule
{
    public string Prefix => "/auth";

    public void Map(RouteGroupBuilder group)
    {
        // S7's anonymous allow-list is signup and login only, and both are rate-limited (S6, plan P28)
        group.MapPost("/signup", async (SignUpCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToCreated())
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithName("SignUp")
            .ProducesValidationProblem()
            // A taken email or slug is a field error
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // A failure is a 422, never a 401, so the SPA's 401 → /login redirect leaves a login form alone
        group.MapPost("/login", async (LogInCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToNoContent())
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithName("LogIn")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // Needs a session: a cross-site POST carries no SameSite=Lax cookie, so it can't sign anyone out
        group.MapPost("/logout", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new LogOutCommand(), ct)).ToNoContent())
            .RequireAuthorization()
            .WithName("LogOut")
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
