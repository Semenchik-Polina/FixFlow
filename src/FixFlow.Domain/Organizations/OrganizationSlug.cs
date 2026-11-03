using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace FixFlow.Domain.Organizations;

/// <summary>
/// The rules for an organization's slug, the first segment of every app URL (<c>/{slug}/...</c>) and the value of the
/// <c>X-Organization</c> header (§8). A slug never changes (A10), so a word a global page or server path might need has
/// to be reserved before any organization can take it.
/// </summary>
public static partial class OrganizationSlug
{
    public const int MinLength = 3;

    public const int MaxLength = 40;

    /// <summary>First path segments that aren't organizations. Slugs are lowercase only, so the comparison is ordinal.</summary>
    public static IReadOnlySet<string> Reserved { get; } = FrozenSet.Create(
        StringComparer.Ordinal,
        // The SPA's global pages (web/src/shared/routing/paths.ts; a test checks that this list covers it)
        "login",
        // Global pages planned by later tasks: the auth pages, the organization picker and creating an organization
        // (T1.7), accepting an invite (T3.4), the user's profile. Their routes take a name from this list.
        "signup",
        "forgot-password",
        "reset-password",
        "invite",
        "account",
        "organizations",
        "new",
        // Phase 2's email confirmation page (ADR 0003)
        "confirm-email",
        // Server paths the SPA fallback leaves alone (FixFlow.API ClientRouteConstraint), and the SPA bundle's folder
        "api",
        "health",
        "hubs",
        "assets");

    /// <summary>
    /// Whether <paramref name="slug"/> can be an organization's slug, as written: it isn't lowercased or trimmed.
    /// It doesn't check whether another organization has it already; the database's unique index does (§10).
    /// </summary>
    /// <param name="slug">The slug to check.</param>
    /// <param name="error">Why it can't, written for the user and without the slug itself; null when it can.</param>
    public static bool IsValid(string slug, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(slug);

        error = Problem(slug);
        return error is null;
    }

    private static string? Problem(string slug)
    {
        if (slug.Length is < MinLength or > MaxLength)
        {
            return $"A slug must be {MinLength} to {MaxLength} characters long.";
        }

        if (!Format().IsMatch(slug))
        {
            return "A slug may contain only lowercase letters, digits and single hyphens, and must start and end with a letter or digit.";
        }

        return Reserved.Contains(slug) ? "This slug is reserved." : null;
    }

    // No dots: the SPA fallback takes a dotted last segment for a file name (T0.11). \z, because $ also matches
    // before a trailing newline.
    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*\z")]
    private static partial Regex Format();
}
