using FixFlow.Application.Results;
using MediatR;

namespace FixFlow.Application.Organizations.CreateOrganization;

/// <summary>A signed-in user creates another organization and becomes its first Admin.</summary>
public sealed record CreateOrganizationCommand(string Name, string Slug) : IRequest<Result<OrganizationCreated>>;
