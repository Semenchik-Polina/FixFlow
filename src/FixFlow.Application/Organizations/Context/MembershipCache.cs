using Microsoft.Extensions.Caching.Hybrid;

namespace FixFlow.Application.Organizations.Context;

/// <summary>
/// The cache of the caller's active membership per organization (§13.1). The expiration is only a safety net: every
/// change to a membership invalidates its user's tag (<see cref="InvalidateMembershipCacheHandler"/>).
/// </summary>
internal static class MembershipCache
{
    public static readonly TimeSpan Expiration = TimeSpan.FromSeconds(60);

    public static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        Expiration = Expiration,
        LocalCacheExpiration = Expiration,
    };

    /// <summary>Per user and slug: slugs never change (plan A10), so no organization change has to invalidate it.</summary>
    public static string Key(Guid userId, string slug) => $"membership:{userId}:{slug}";

    /// <summary>Every entry of the user: a change to any of their memberships drops them all.</summary>
    public static string Tag(Guid userId) => $"user:{userId}:memberships";
}
