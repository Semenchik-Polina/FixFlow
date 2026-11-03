using System.Net;
using System.Text.RegularExpressions;
using FixFlow.Application.Email;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>The reset link in a password reset email, read the way the SPA reads it (T1.7).</summary>
public static partial class ResetLink
{
    private const string TokenPrefix = "#token=";

    /// <summary>The one reset link in the message's text body; its HTML body must link to the same address.</summary>
    public static Uri From(EmailMessage message)
    {
        var link = In(message.TextBody);
        Assert.Contains($"href=\"{WebUtility.HtmlEncode(link.AbsoluteUri)}\"", message.HtmlBody, StringComparison.Ordinal);
        return link;
    }

    /// <summary>The one reset link in an email body, such as Mailpit's copy of the text body.</summary>
    public static Uri In(string body)
    {
        var match = Assert.Single(Link().Matches(body));
        return new Uri(match.Value, UriKind.Absolute);
    }

    /// <summary>
    /// The code the SPA posts back (<c>{ token }</c>). Asserts it's in the fragment, which browsers never send, and not
    /// in the query, which reaches servers' logs (A11).
    /// </summary>
    public static string Token(Uri link)
    {
        Assert.Equal("/reset-password", link.AbsolutePath);
        Assert.Empty(link.Query);
        Assert.StartsWith(TokenPrefix, link.Fragment, StringComparison.Ordinal);
        var token = Uri.UnescapeDataString(link.Fragment[TokenPrefix.Length..]);
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token;
    }

    /// <summary>The code from the newest reset email that <paramref name="sink"/> holds for <paramref name="email"/>.</summary>
    public static string LatestToken(InMemoryEmailSink sink, string email)
    {
        var messages = sink.To(email);
        Assert.NotEmpty(messages);
        return Token(From(messages[^1]));
    }

    [GeneratedRegex(@"https?://\S+/reset-password\S*")]
    private static partial Regex Link();
}
