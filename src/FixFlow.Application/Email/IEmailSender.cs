namespace FixFlow.Application.Email;

/// <summary>
/// Sends email (SMTP to Mailpit locally, D21). Called from post-commit handlers, which catch and log failures
/// (ADR 0005). Implementations never log the address or the content: IDs only (S26).
/// The MailKit implementation and the templates arrive with T1.4; tests replace it with an in-memory sink.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
