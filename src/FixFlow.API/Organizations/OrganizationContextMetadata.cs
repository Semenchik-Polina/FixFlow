namespace FixFlow.API.Organizations;

/// <summary>
/// Whether an endpoint needs an organization context (§8). Added with
/// <see cref="OrganizationContextExtensions.RequireOrganization{TBuilder}"/>, the default of <c>/api/v1</c>, and
/// <see cref="OrganizationContextExtensions.WithoutOrganization{TBuilder}"/>. An endpoint can get both, from its groups
/// and from itself: the last one wins (<c>GetMetadata</c>), so a module's overrides the root's and an endpoint's own
/// overrides its module's, as with <c>AllowAnonymous</c>. An endpoint with neither (health, the SPA fallback) needs no
/// context.
/// </summary>
public sealed class OrganizationContextMetadata
{
    private OrganizationContextMetadata(bool isRequired) => IsRequired = isRequired;

    /// <summary>A signed-in caller must name an organization they are an active member of.</summary>
    public static OrganizationContextMetadata Required { get; } = new(isRequired: true);

    /// <summary>Organization-less: the header is ignored, and the request gets no organization context.</summary>
    public static OrganizationContextMetadata NotRequired { get; } = new(isRequired: false);

    public bool IsRequired { get; }
}
