using FixFlow.Application.Email;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Utils;

namespace FixFlow.Infrastructure.Email;

/// <summary>
/// Sends each message over its own SMTP connection, which ends with <c>QUIT</c>. A failure is thrown as
/// <see cref="EmailDeliveryException"/> (<see cref="MailKitFailures"/>). It logs nothing: the caller logs, with IDs.
/// </summary>
internal sealed class MailKitEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        // Checks the settings on first use (SmtpOptions)
        var smtp = options.Value;
        using var mime = ToMime(message, smtp);
        using var client = new SmtpClient { Timeout = (int)smtp.Timeout.TotalMilliseconds };

        try
        {
            await client.ConnectAsync(smtp.Host, smtp.Port, smtp.Security, cancellationToken);
            // The settings require both or neither
            if (smtp is { UserName: { Length: > 0 } userName, Password: { } password })
            {
                await client.AuthenticateAsync(userName, password, cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
        }
        // MailKit's own timeout, not the caller's cancellation
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EmailDeliveryException(EmailFailureKind.Timeout, exception);
        }
        catch (Exception exception) when (MailKitFailures.ToDeliveryException(exception) is { } failure)
        {
            throw failure;
        }
    }

    private static MimeMessage ToMime(EmailMessage message, SmtpOptions smtp)
    {
        var from = new MailboxAddress(smtp.FromName, smtp.FromAddress);
        var body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody };

        return new MimeMessage
        {
            From = { from },
            To = { Recipient(message) },
            Subject = message.Subject,
            // From the sender's domain, not this machine's name, which MimeKit would use otherwise
            MessageId = MimeUtils.GenerateMessageId(from.Domain),
            // multipart/alternative when there is an HTML body, text/plain otherwise
            Body = body.ToMessageBody(),
        };
    }

    /// <summary>
    /// The recipient, only as the bare address it is. MimeKit reads <c>Name &lt;someone@example.com&gt;</c> as a display
    /// name and an address, which would send the mail to an inbox other than the one stored. The email rule rejects
    /// that form (<c>IdentityValidationRules.Email</c>); this check covers every caller.
    /// </summary>
    private static MailboxAddress Recipient(EmailMessage message) =>
        MailboxAddress.TryParse(message.To, out var mailbox)
        && string.Equals(mailbox.Address, message.To, StringComparison.Ordinal)
            ? mailbox
            : throw new ArgumentException("The recipient isn't a bare email address.", nameof(message));
}
