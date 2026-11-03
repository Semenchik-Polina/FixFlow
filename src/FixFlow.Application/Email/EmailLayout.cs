using System.Net;
using System.Text;

namespace FixFlow.Application.Email;

/// <summary>
/// Builds an email's text and HTML bodies from paragraphs and links, with the same content in both. The HTML body
/// encodes every value, so a name like <c>&lt;b&gt;Jo&lt;/b&gt;</c> shows as typed. It holds no wording: each email's
/// factory, next to its use case, has its own strings (D27).
/// </summary>
public sealed class EmailLayout
{
    private readonly List<string> _text = [];
    private readonly List<string> _html = [];

    public EmailLayout Paragraph(string text)
    {
        _text.Add(text);
        _html.Add($"<p>{WebUtility.HtmlEncode(text)}</p>");
        return this;
    }

    /// <summary>A link in a paragraph of its own. The text body shows the label, then the address below it.</summary>
    /// <exception cref="ArgumentException"><paramref name="url"/> isn't an absolute http(s) URL.</exception>
    public EmailLayout Link(string label, Uri url)
    {
        if (!url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("An email link must be an absolute http(s) URL.", nameof(url));
        }

        var address = url.AbsoluteUri;
        _text.Add($"{label}:\n{address}");
        _html.Add($"<p><a href=\"{WebUtility.HtmlEncode(address)}\">{WebUtility.HtmlEncode(label)}</a></p>");
        return this;
    }

    public EmailMessage ToMessage(string to, string subject) =>
        new(to, subject, TextBody(), HtmlBody(subject));

    // Paragraphs apart by a blank line; '\n' on every OS, so the body doesn't depend on where it was built
    private string TextBody() => string.Join("\n\n", _text) + "\n";

    private string HtmlBody(string subject) => new StringBuilder()
        .Append("<!DOCTYPE html>\n")
        .Append("<html lang=\"en\">\n")
        .Append("<head>\n")
        .Append("<meta charset=\"utf-8\">\n")
        .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
        .Append("<title>").Append(WebUtility.HtmlEncode(subject)).Append("</title>\n")
        .Append("</head>\n")
        .Append("<body style=\"font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.5;\">\n")
        .AppendJoin('\n', _html).Append('\n')
        .Append("</body>\n")
        .Append("</html>\n")
        .ToString();
}
