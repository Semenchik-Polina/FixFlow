using FixFlow.Application.Email;
using FixFlow.Application.Identity.ForgotPassword;
using FixFlow.Application.Identity.ResetPassword;
using NodaTime;

namespace FixFlow.Application.Tests.Identity;

public class ResetPasswordEmailTests
{
    private const string Recipient = "user@example.test";

    private static readonly Uri _link = new("https://fixflow.example.test/reset-password#token=user-id.reset_code");

    [Fact]
    public void Create_PutsTheLinkInBothBodies()
    {
        var message = Create("Ada");

        Assert.Equal(Recipient, message.To);
        Assert.Equal(ResetPasswordEmail.Subject, message.Subject);
        Assert.Contains(_link.AbsoluteUri, message.TextBody, StringComparison.Ordinal);
        Assert.Contains($"href=\"{_link.AbsoluteUri}\"", message.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_CodeLifespan_SaysAnHour()
    {
        // S4's hour, which the token provider enforces; the reset tests pin it there too
        var message = ResetPasswordEmail.Create(Recipient, "Ada", _link, ResetPasswordCode.Lifespan);

        Assert.Contains("The link works once, within an hour.", message.TextBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, "a minute")]
    [InlineData(30, "30 minutes")]
    [InlineData(60, "an hour")]
    [InlineData(90, "90 minutes")]
    [InlineData(120, "2 hours")]
    [InlineData(72 * 60, "72 hours")]
    public void Create_Lifespan_IsStatedInWords(int minutes, string words)
    {
        var message = ResetPasswordEmail.Create(Recipient, "Ada", _link, Duration.FromMinutes(minutes));

        Assert.Contains($"within {words}.", message.TextBody, StringComparison.Ordinal);
        Assert.Contains($"within {words}.", message.HtmlBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    [InlineData(90)]
    public void Create_LifespanNotWholeMinutes_Throws(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ResetPasswordEmail.Create(Recipient, "Ada", _link, Duration.FromSeconds(seconds)));
    }

    [Fact]
    public void Create_NameIsHtmlEncoded()
    {
        var message = Create("<b>Ada</b>");

        Assert.Contains("Hi &lt;b&gt;Ada&lt;/b&gt;,", message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Hi <b>Ada</b>,", message.TextBody, StringComparison.Ordinal);
    }

    private static EmailMessage Create(string firstName) =>
        ResetPasswordEmail.Create(Recipient, firstName, _link, Duration.FromHours(1));
}
