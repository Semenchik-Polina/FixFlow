using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Identity.GetMe;

/// <summary>The caller's profile and active memberships. No input, so no validator.</summary>
public sealed record GetMeQuery : IRequest<Result<MeResponse>>;
