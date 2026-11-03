using System.ComponentModel.DataAnnotations;

namespace FixFlow.API.Spa;

/// <summary>
/// Where users open the SPA, from <c>Spa</c>. It has no safe default, so it is checked when the first link is built,
/// not at startup: the build-time OpenAPI run needs no settings.
/// </summary>
public sealed class SpaLinkOptions
{
    public const string SectionName = "Spa";

    /// <summary>
    /// The SPA's public address as users' browsers reach it, e.g. <c>https://fixflow.example</c>: absolute, http(s),
    /// no query or fragment. A path is allowed, for an SPA served below one.
    /// </summary>
    [Required]
    public Uri? PublicUrl { get; set; }

    // In this order: a relative URI has no scheme, query or fragment to read
    internal static bool IsValid(SpaLinkOptions options) =>
        options.PublicUrl is { IsAbsoluteUri: true } url
        && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp)
        && url.Query.Length == 0
        && url.Fragment.Length == 0;
}
