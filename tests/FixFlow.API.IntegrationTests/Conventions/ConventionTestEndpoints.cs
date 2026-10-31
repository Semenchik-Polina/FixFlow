using FixFlow.API.Endpoints;
using FixFlow.API.Organizations;
using FixFlow.Application.Paging;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FixFlow.API.IntegrationTests.Conventions;

/// <summary>
/// Test-only endpoints (registered by <see cref="Fixtures.FixFlowApiFactory"/>) that run the API conventions through
/// the app's real pipeline: MediatR + validation, Result → TypedResults, exceptions → Problem Details, JSON and the
/// fallback policy. Everything is anonymous except <c>/protected</c>: there is no sign-in before T1.3. Organization-less,
/// so signed-in callers need no <c>X-Organization</c> header either.
/// </summary>
public sealed class ConventionTestEndpoints : IEndpointModule
{
    public const string Path = $"{EndpointModuleExtensions.ApiRoot}/test-conventions";

    /// <summary>The message of the exception behind <c>/crash</c>; it must never reach the response.</summary>
    public const string CrashDetail = "Connection to db-internal-7 failed for user fixflow_app";

    public string Prefix => "/test-conventions";

    public void Map(RouteGroupBuilder group)
    {
        group.WithoutOrganization();

        group.MapGet("/samples/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new FindSample(id), ct)).ToOk())
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/samples/current", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetCurrentSample(), ct)).ToOk())
            .AllowAnonymous();

        group.MapPost("/samples", async (EchoSample command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToOk())
            .AllowAnonymous()
            .ProducesValidationProblem();

        group.MapGet("/samples", async (ISender sender, CancellationToken ct, int page = 1, int pageSize = PageSizes.Default) =>
                (await sender.Send(new ListSamples(page, pageSize), ct)).ToOk())
            .AllowAnonymous()
            .ProducesValidationProblem();

        group.MapPost("/domain-rule", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new BreakDomainRule(), ct)).ToNoContent())
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/assignments", async (AssignTechnician command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToNoContent())
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/stale", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new UpdateStaleSample(), ct)).ToNoContent())
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/crash", string () => throw new InvalidOperationException(CrashDetail))
            .AllowAnonymous();

        // No AllowAnonymous: the fallback policy applies
        group.MapGet("/protected", () => TypedResults.NoContent());
    }
}
