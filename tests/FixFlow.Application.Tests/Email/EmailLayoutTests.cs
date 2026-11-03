using FixFlow.Application.Email;

namespace FixFlow.Application.Tests.Email;

public class EmailLayoutTests
{
    private const string Recipient = "user@example.test";

    [Fact]
    public void Paragraph_MarkupInAValue_IsEncodedInHtmlAndKeptInText()
    {
        var message = new EmailLayout()
            .Paragraph("Hi <b>Jo</b> & \"Al\" 'O'")
            .ToMessage(Recipient, "Subject");

        Assert.Contains(
            "<p>Hi &lt;b&gt;Jo&lt;/b&gt; &amp; &quot;Al&quot; &#39;O&#39;</p>",
            message.HtmlBody,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", message.HtmlBody, StringComparison.Ordinal);
        Assert.Equal("Hi <b>Jo</b> & \"Al\" 'O'\n", message.TextBody);
    }

    [Fact]
    public void Link_LabelAndAddress_AreEncodedInHtml()
    {
        var message = new EmailLayout()
            .Link("Reset <now>", new Uri("https://fixflow.example.test/reset-password?a=1&b=2#token=x.y"))
            .ToMessage(Recipient, "Subject");

        Assert.Contains(
            "<p><a href=\"https://fixflow.example.test/reset-password?a=1&amp;b=2#token=x.y\">"
            + "Reset &lt;now&gt;</a></p>",
            message.HtmlBody,
            StringComparison.Ordinal);
        Assert.Equal(
            "Reset <now>:\nhttps://fixflow.example.test/reset-password?a=1&b=2#token=x.y\n", message.TextBody);
    }

    [Fact]
    public void ToMessage_Subject_IsEncodedInTheTitle()
    {
        var message = new EmailLayout().Paragraph("Body").ToMessage(Recipient, "Tom & Jerry");

        Assert.Equal("Tom & Jerry", message.Subject);
        Assert.Contains("<title>Tom &amp; Jerry</title>", message.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void ToMessage_SeveralBlocks_AreInOrder_ApartByABlankLineInText()
    {
        var message = new EmailLayout()
            .Paragraph("First")
            .Link("Open", new Uri("https://fixflow.example.test/"))
            .Paragraph("Last")
            .ToMessage(Recipient, "Subject");

        Assert.Equal(Recipient, message.To);
        Assert.Equal("First\n\nOpen:\nhttps://fixflow.example.test/\n\nLast\n", message.TextBody);
        Assert.Contains(
            "<p>First</p>\n<p><a href=\"https://fixflow.example.test/\">Open</a></p>\n<p>Last</p>\n",
            message.HtmlBody,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:user@example.test")]
    [InlineData("/reset-password")]
    public void Link_NotAnAbsoluteHttpUrl_Throws(string url)
    {
        var layout = new EmailLayout();

        Assert.Throws<ArgumentException>(() => layout.Link("Open", new Uri(url, UriKind.RelativeOrAbsolute)));
    }
}
