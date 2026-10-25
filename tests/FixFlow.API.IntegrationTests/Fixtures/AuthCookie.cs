using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Net.Http.Headers;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>Identity's application cookie, which carries the session.</summary>
public static class AuthCookie
{
    /// <summary>Cookie authentication names its cookie after the scheme.</summary>
    public static readonly string Name = CookieAuthenticationDefaults.CookiePrefix + IdentityConstants.ApplicationScheme;

    /// <summary>The response's <c>Set-Cookie</c> for the auth cookie, or null when it sets none.</summary>
    public static SetCookieHeaderValue? Find(HttpResponseMessage response) =>
        response.Headers.TryGetValues(HeaderNames.SetCookie, out var headers)
            ? SetCookieHeaderValue.ParseList([.. headers]).SingleOrDefault(cookie => cookie.Name.Equals(Name))
            : null;
}
