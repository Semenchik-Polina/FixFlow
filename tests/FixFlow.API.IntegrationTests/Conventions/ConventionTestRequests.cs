using FixFlow.Application.Errors;
using FixFlow.Application.Paging;
using FixFlow.Application.Results;
using FixFlow.Domain;
using FluentValidation;
using MediatR;
using NodaTime;

namespace FixFlow.API.IntegrationTests.Conventions;

// Requests, handlers and validators behind ConventionTestEndpoints, registered from this assembly by the factory

public enum SampleStatus
{
    New,
    InProgress,
    OnHold,
}

public sealed record Sample(
    string Name,
    Instant At,
    LocalDate On,
    LocalTime OpensAt,
    LocalDateTime VisitAt,
    DateTimeZone TimeZone,
    Instant? NotSet,
    SampleStatus Status,
    IReadOnlyList<string> Tags);

public sealed record FindSample(Guid Id) : IRequest<Result<Sample>>;

internal sealed class FindSampleHandler : IRequestHandler<FindSample, Result<Sample>>
{
    public Task<Result<Sample>> Handle(FindSample request, CancellationToken cancellationToken) =>
        Task.FromResult<Result<Sample>>(Result.NotFound());
}

public sealed record GetCurrentSample : IRequest<Result<Sample>>;

internal sealed class GetCurrentSampleHandler(IClock clock) : IRequestHandler<GetCurrentSample, Result<Sample>>
{
    public Task<Result<Sample>> Handle(GetCurrentSample request, CancellationToken cancellationToken)
    {
        var now = clock.GetCurrentInstant();
        var zone = DateTimeZoneProviders.Tzdb["Europe/Warsaw"];
        var local = now.InZone(zone).LocalDateTime;
        return Task.FromResult<Result<Sample>>(new Sample(
            "Current", now, now.InUtc().Date, local.TimeOfDay, local, zone, null, SampleStatus.InProgress, ["boiler"]));
    }
}

public sealed record EchoSample(
    string Name,
    Instant At,
    LocalDate On,
    LocalTime OpensAt,
    LocalDateTime VisitAt,
    DateTimeZone TimeZone,
    Instant? NotSet,
    SampleStatus Status,
    IReadOnlyList<string> Tags) : IRequest<Result<Sample>>;

internal sealed class EchoSampleValidator : AbstractValidator<EchoSample>
{
    public EchoSampleValidator()
    {
        RuleFor(command => command.Name).NotEmpty();
        RuleForEach(command => command.Tags).NotEmpty();
    }
}

internal sealed class EchoSampleHandler : IRequestHandler<EchoSample, Result<Sample>>
{
    public Task<Result<Sample>> Handle(EchoSample request, CancellationToken cancellationToken) =>
        Task.FromResult<Result<Sample>>(new Sample(
            request.Name,
            request.At,
            request.On,
            request.OpensAt,
            request.VisitAt,
            request.TimeZone,
            request.NotSet,
            request.Status,
            request.Tags));
}

public sealed record ListSamples(int Page, int PageSize) : IRequest<Result<PagedResult<int>>>, IPagedQuery
{
    public const int TotalCount = 250;
}

internal sealed class ListSamplesValidator : PagedQueryValidator<ListSamples>;

internal sealed class ListSamplesHandler : IRequestHandler<ListSamples, Result<PagedResult<int>>>
{
    public Task<Result<PagedResult<int>>> Handle(ListSamples request, CancellationToken cancellationToken)
    {
        var items = Enumerable.Range(1, ListSamples.TotalCount)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();
        return Task.FromResult<Result<PagedResult<int>>>(
            new PagedResult<int>(items, request.Page, request.PageSize, ListSamples.TotalCount));
    }
}

public sealed record BreakDomainRule : IRequest<Result>
{
    public const string Message = "Only an assigned work order can be started.";
}

internal sealed class BreakDomainRuleHandler : IRequestHandler<BreakDomainRule, Result>
{
    public Task<Result> Handle(BreakDomainRule request, CancellationToken cancellationToken) =>
        throw new DomainException(BreakDomainRule.Message);
}

public sealed record AssignTechnician(Guid TechnicianId) : IRequest<Result>
{
    public const string Message = "No technician with this id.";
}

internal sealed class AssignTechnicianHandler : IRequestHandler<AssignTechnician, Result>
{
    public Task<Result> Handle(AssignTechnician request, CancellationToken cancellationToken) =>
        Task.FromResult<Result>(Result.FieldError(nameof(request.TechnicianId), AssignTechnician.Message));
}

public sealed record UpdateStaleSample : IRequest<Result>;

internal sealed class UpdateStaleSampleHandler : IRequestHandler<UpdateStaleSample, Result>
{
    public Task<Result> Handle(UpdateStaleSample request, CancellationToken cancellationToken) =>
        throw new ConcurrencyConflictException();
}
