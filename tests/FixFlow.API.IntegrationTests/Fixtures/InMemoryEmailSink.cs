using System.Collections.Concurrent;
using FixFlow.Application.Email;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>
/// Captures mail instead of sending it. Post-commit handlers send within the request, so the mail is here when the
/// response arrives. Read only by recipient: each test uses its own addresses, so parallel tests never see each
/// other's mail.
/// </summary>
public sealed class InMemoryEmailSink : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public IReadOnlyList<EmailMessage> To(string address) =>
        _messages.Where(message => string.Equals(message.To, address, StringComparison.OrdinalIgnoreCase)).ToList();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }
}
