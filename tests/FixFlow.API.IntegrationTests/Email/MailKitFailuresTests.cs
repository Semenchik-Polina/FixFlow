using System.Net.Sockets;
using FixFlow.Application.Email;
using FixFlow.Infrastructure.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace FixFlow.API.IntegrationTests.Email;

/// <summary>
/// The translation of MailKit's exceptions, without a server: real servers quote the address in their replies, which
/// Mailpit's chaos replies don't.
/// </summary>
public class MailKitFailuresTests
{
    private const string Address = "jane.doe@example.test";

    [Fact]
    public void ToDeliveryException_CommandQuotingTheAddress_KeepsStatusAndCodeOnly()
    {
        var command = new SmtpCommandException(
            SmtpErrorCode.RecipientNotAccepted,
            SmtpStatusCode.MailboxUnavailable,
            new MailboxAddress("Jane Doe", Address),
            $"5.1.1 <{Address}>: Recipient address rejected: User unknown");

        var failure = MailKitFailures.ToDeliveryException(command);

        Assert.NotNull(failure);
        Assert.Equal(EmailFailureKind.Command, failure.Kind);
        Assert.Equal(550, failure.SmtpStatus);
        Assert.Equal("MailboxUnavailable", failure.SmtpStatusName);
        Assert.Equal("RecipientNotAccepted", failure.SmtpErrorCode);
        Assert.Equal("SMTP command failed: 550 MailboxUnavailable (RecipientNotAccepted).", failure.Message);
        Assert.Same(command, failure.InnerException);
        Assert.DoesNotContain("jane", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToDeliveryException_StatusMailKitDoesNotName_KeepsTheNumber()
    {
        var command = new SmtpCommandException(
            SmtpErrorCode.UnexpectedStatusCode, (SmtpStatusCode)599, $"Go away, {Address}");

        var failure = MailKitFailures.ToDeliveryException(command);

        Assert.NotNull(failure);
        Assert.Null(failure.SmtpStatusName);
        Assert.Equal("SMTP command failed: 599 (UnexpectedStatusCode).", failure.Message);
    }

    public static TheoryData<Exception, EmailFailureKind> Failures => new()
    {
        { new SocketException((int)SocketError.ConnectionRefused), EmailFailureKind.Connect },
        { new IOException($"Connection to {Address} dropped"), EmailFailureKind.Connect },
        { new SslHandshakeException($"Certificate for {Address}"), EmailFailureKind.Tls },
        { new AuthenticationException($"535 {Address}: bad credentials"), EmailFailureKind.Authentication },
        { new SmtpProtocolException($"Unexpected reply about {Address}"), EmailFailureKind.Protocol },
        { new TimeoutException($"No answer for {Address}"), EmailFailureKind.Timeout },
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public void ToDeliveryException_TransportFailure_KeepsTheKindOnly(Exception exception, EmailFailureKind kind)
    {
        var failure = MailKitFailures.ToDeliveryException(exception);

        Assert.NotNull(failure);
        Assert.Equal(kind, failure.Kind);
        Assert.Null(failure.SmtpStatus);
        Assert.Same(exception, failure.InnerException);
        Assert.DoesNotContain(Address, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToDeliveryException_Cancellation_IsNotTranslated()
    {
        Assert.Null(MailKitFailures.ToDeliveryException(new OperationCanceledException()));
    }

    [Fact]
    public void ToDeliveryException_OtherException_IsNotTranslated()
    {
        Assert.Null(MailKitFailures.ToDeliveryException(new InvalidOperationException()));
    }
}
