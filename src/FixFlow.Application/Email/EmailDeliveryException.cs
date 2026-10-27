using System.Globalization;
using FixFlow.Application.Errors;

namespace FixFlow.Application.Email;

/// <summary>
/// An <see cref="IEmailSender"/> couldn't deliver a message. The original exception is the inner exception. Its
/// message is built from <see cref="Kind"/> and the SMTP status only, never from the server's reply, which can quote
/// the address ("550 5.1.1 &lt;jane.doe@…&gt;: Recipient address rejected"), so logs keep it (ADR 0007, S26).
/// </summary>
public sealed class EmailDeliveryException : Exception, ILogSafeMessage
{
    /// <summary>A failure without an SMTP status: no connection, TLS, credentials, protocol or timeout.</summary>
    public EmailDeliveryException(EmailFailureKind kind, Exception innerException)
        : base(Describe(kind), innerException)
    {
        Kind = kind;
    }

    /// <summary>The server answered a command with an error status (<see cref="EmailFailureKind.Command"/>).</summary>
    /// <param name="smtpStatus">The reply code, e.g. 550.</param>
    /// <param name="smtpStatusName">
    /// The SMTP library's name for the code (<c>MailboxUnavailable</c>), or null for a code it doesn't name. An
    /// identifier, never the server's text.
    /// </param>
    /// <param name="smtpErrorCode">
    /// The SMTP library's name for the step that failed (<c>RecipientNotAccepted</c>). An identifier, never the
    /// server's text.
    /// </param>
    /// <param name="innerException">The original exception.</param>
    public EmailDeliveryException(
        int smtpStatus, string? smtpStatusName, string smtpErrorCode, Exception innerException)
        : base(DescribeCommand(smtpStatus, smtpStatusName, smtpErrorCode), innerException)
    {
        Kind = EmailFailureKind.Command;
        SmtpStatus = smtpStatus;
        SmtpStatusName = smtpStatusName;
        SmtpErrorCode = smtpErrorCode;
    }

    public EmailFailureKind Kind { get; }

    /// <summary>The server's reply code, when it answered with an error.</summary>
    public int? SmtpStatus { get; }

    /// <summary>The SMTP library's name for <see cref="SmtpStatus"/>, when it has one.</summary>
    public string? SmtpStatusName { get; }

    /// <summary>The SMTP library's name for the step that failed, when the server answered with an error.</summary>
    public string? SmtpErrorCode { get; }

    private static string Describe(EmailFailureKind kind) => kind switch
    {
        EmailFailureKind.Connect => "Could not connect to the SMTP server.",
        EmailFailureKind.Tls => "The TLS handshake with the SMTP server failed.",
        EmailFailureKind.Authentication => "The SMTP server rejected the credentials.",
        EmailFailureKind.Command => "An SMTP command failed.",
        EmailFailureKind.Protocol => "The SMTP server's reply didn't follow the protocol.",
        EmailFailureKind.Timeout => "The SMTP server didn't answer in time.",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static string DescribeCommand(int status, string? statusName, string errorCode) => statusName is null
        ? string.Create(CultureInfo.InvariantCulture, $"SMTP command failed: {status} ({errorCode}).")
        : string.Create(CultureInfo.InvariantCulture, $"SMTP command failed: {status} {statusName} ({errorCode}).");
}
