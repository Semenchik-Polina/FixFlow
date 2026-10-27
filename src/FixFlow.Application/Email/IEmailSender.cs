namespace FixFlow.Application.Email;

/// <summary>
/// Sends email (SMTP to Mailpit locally, D21). A message that can't be delivered throws
/// <see cref="EmailDeliveryException"/>. Callers catch failures and log them through
/// <see cref="Errors.IExceptionRedactor"/>: post-commit handlers through the dispatcher (ADR 0005), a handler that
/// sends inline itself. Implementations never log the address or the content: IDs only (S26).
/// Tests replace it with an in-memory sink.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
