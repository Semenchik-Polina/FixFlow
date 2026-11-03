using System.Net.Sockets;
using FixFlow.Application.Email;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace FixFlow.Infrastructure.Email;

/// <summary>
/// Translates MailKit's exceptions into <see cref="EmailDeliveryException"/>. Only types, status codes and enum names
/// are read: the messages and <see cref="SmtpCommandException.Mailbox"/> can hold the address, so they stay in the
/// inner exception, which logs redact (ADR 0007).
/// </summary>
public static class MailKitFailures
{
    /// <summary>
    /// The delivery failure <paramref name="exception"/> stands for, or null for one that isn't a delivery failure. A
    /// cancellation is never translated: the caller decides whether it cancelled.
    /// </summary>
    public static EmailDeliveryException? ToDeliveryException(Exception exception) => exception switch
    {
        SmtpCommandException command => new EmailDeliveryException(
            (int)command.StatusCode,
            Enum.IsDefined(command.StatusCode) ? command.StatusCode.ToString() : null,
            command.ErrorCode.ToString(),
            command),
        // SaslException too; ServiceNotAuthenticatedException is the server requiring credentials that weren't given
        AuthenticationException or ServiceNotAuthenticatedException =>
            new EmailDeliveryException(EmailFailureKind.Authentication, exception),
        SslHandshakeException => new EmailDeliveryException(EmailFailureKind.Tls, exception),
        SmtpProtocolException => new EmailDeliveryException(EmailFailureKind.Protocol, exception),
        TimeoutException => new EmailDeliveryException(EmailFailureKind.Timeout, exception),
        SocketException or IOException or ServiceNotConnectedException =>
            new EmailDeliveryException(EmailFailureKind.Connect, exception),
        // MailKit's answer when the server lacks STARTTLS, AUTH or SMTPUTF8
        NotSupportedException => new EmailDeliveryException(EmailFailureKind.Unsupported, exception),
        _ => null,
    };
}
