namespace FixFlow.Application.Identity.GetMe;

/// <summary>The caller's profile, and the organizations they may enter.</summary>
/// <param name="Memberships">Active only, ordered by the organization's name.</param>
public sealed record MeResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<MyMembership> Memberships);
