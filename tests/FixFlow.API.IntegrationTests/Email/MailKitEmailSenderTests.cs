using System.Net.Sockets;
using FixFlow.API.IntegrationTests.Fixtures;
using FixFlow.Application.Email;
using MailKit.Net.Smtp;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.API.IntegrationTests.Email;

/// <summary>The app's SMTP sender against Mailpit, a Mailpit that rejects every recipient, and no server.</summary>
public class MailKitEmailSenderTests(FixFlowApiFactory api)
{
    [Fact]
    public async Task Send_ToMailpit_DeliversTextAndHtml()
    {
        await using var app = api.WithRealEmail(api.MailpitSmtp);
        var recipient = TestData.Email();
        var message = new EmailLayout()
            .Paragraph("Hello <there> & welcome.")
            .Link("Open FixFlow", new Uri("https://fixflow.example.test/page?a=1&b=2#token=abc.def"))
            .ToMessage(recipient, "A test message");
        var cancellationToken = TestContext.Current.CancellationToken;

        await app.Services.GetRequiredService<IEmailSender>().SendAsync(message, cancellationToken);

        var delivered = await api.Mailpit.SingleToAsync(recipient, cancellationToken);
        Assert.Equal("A test message", delivered.Subject);
        Assert.Equal(new MailpitAddress("FixFlow", "no-reply@fixflow.example.test"), delivered.From);
        Assert.Equal(recipient, Assert.Single(delivered.To).Address);
        Assert.Equal(Lines(message.TextBody), Lines(delivered.Text));
        Assert.Equal(Lines(message.HtmlBody!), Lines(delivered.Html));
        Assert.StartsWith(
            "multipart/alternative",
            await api.Mailpit.HeaderAsync(delivered, "Content-Type", cancellationToken),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_IdnDomain_Delivers()
    {
        // MimeKit decodes the domain to münchen; the stored form must still count as the address it is
        await using var app = api.WithRealEmail(api.MailpitSmtp);
        var recipient = TestData.Email().Replace("@example.test", "@xn--mnchen-3ya.example.test", StringComparison.Ordinal);
        var cancellationToken = TestContext.Current.CancellationToken;

        await app.Services.GetRequiredService<IEmailSender>().SendAsync(
            new EmailMessage(recipient, "Subject", "Body"), cancellationToken);

        var delivered = await api.Mailpit.SingleToAsync(recipient, cancellationToken);
        Assert.Equal(recipient, Assert.Single(delivered.To).Address);
    }

    [Fact]
    public async Task Send_RecipientRejected_ThrowsDeliveryExceptionWithStatus()
    {
        await using var app = api.WithRealEmail(api.RejectingSmtp);
        var recipient = TestData.Email();
        var sender = app.Services.GetRequiredService<IEmailSender>();

        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(() => sender.SendAsync(
            new EmailMessage(recipient, "Subject", "Body"), TestContext.Current.CancellationToken));

        Assert.Equal(EmailFailureKind.Command, failure.Kind);
        Assert.Equal(550, failure.SmtpStatus);
        Assert.Equal("RecipientNotAccepted", failure.SmtpErrorCode);
        Assert.Equal("SMTP command failed: 550 MailboxUnavailable (RecipientNotAccepted).", failure.Message);
        Assert.IsType<SmtpCommandException>(failure.InnerException);
        Assert.DoesNotContain(recipient, failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Send_ServerDown_ThrowsConnectKind()
    {
        await using var app = api.WithRealEmail(SmtpEndpoint.Closed());
        var sender = app.Services.GetRequiredService<IEmailSender>();

        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(() => sender.SendAsync(
            new EmailMessage(TestData.Email(), "Subject", "Body"), TestContext.Current.CancellationToken));

        Assert.Equal(EmailFailureKind.Connect, failure.Kind);
        Assert.Null(failure.SmtpStatus);
        var socket = Assert.IsType<SocketException>(failure.InnerException);
        Assert.Equal(SocketError.ConnectionRefused, socket.SocketErrorCode);
    }

    [Theory]
    [InlineData("Mallory <victim@example.test>")]
    [InlineData("<victim@example.test>")]
    [InlineData("victim@example.test (Mallory)")]
    [InlineData("victim@example.test(Mallory)")]
    // MimeKit can't read it
    [InlineData("a,b@example.test")]
    public async Task Send_RecipientNotABareAddress_ThrowsBeforeConnecting(string recipient)
    {
        // No server: a connection attempt would fail as Connect instead
        await using var app = api.WithRealEmail(SmtpEndpoint.Closed());
        var sender = app.Services.GetRequiredService<IEmailSender>();

        var failure = await Assert.ThrowsAsync<ArgumentException>(() => sender.SendAsync(
            new EmailMessage(recipient, "Subject", "Body"), TestContext.Current.CancellationToken));

        Assert.Equal("message", failure.ParamName);
    }

    // SMTP sends CRLF; the message was built with LF
    private static string Lines(string body) => body.ReplaceLineEndings("\n").TrimEnd('\n');
}
