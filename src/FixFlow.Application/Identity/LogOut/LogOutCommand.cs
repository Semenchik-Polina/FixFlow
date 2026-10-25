using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Identity.LogOut;

/// <summary>Signs the caller out of this browser. No input, so no validator.</summary>
public sealed record LogOutCommand : IRequest<Result>;
