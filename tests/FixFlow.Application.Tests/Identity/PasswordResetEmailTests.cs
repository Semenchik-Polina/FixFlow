using FixFlow.Application.Identity.ForgotPassword;

namespace FixFlow.Application.Tests.Identity;

public class PasswordResetEmailTests
{
    private const string Recipient = "user@example.test";

    private static readonly Uri _link = new("https://fixflow.example.test/reset-password#token=user-id.reset_code");

    [Fact]
    public void Create_PutsTheLinkInBothBodies()
    {
        var message = PasswordResetEmail.Create(Recipient, "Ada", _link);

        Assert.Equal(Recipient, message.To);
        Assert.Equal(PasswordResetEmail.Subject, message.Subject);
        Assert.Contains(_link.AbsoluteUri, message.TextBody, StringComparison.Ordinal);
        Assert.Contains($"href=\"{_link.AbsoluteUri}\"", message.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_SaysHowLongTheLinkWorks()
    {
        // S4's lifespan; the reset tests pin the token provider to the same hour
        var message = PasswordResetEmail.Create(Recipient, "Ada", _link);

        Assert.Contains("within an hour", message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_NameIsHtmlEncoded()
    {
        var message = PasswordResetEmail.Create(Recipient, "<b>Ada</b>", _link);

        Assert.Contains("Hi &lt;b&gt;Ada&lt;/b&gt;,", message.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", message.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Hi <b>Ada</b>,", message.TextBody, StringComparison.Ordinal);
    }
}
