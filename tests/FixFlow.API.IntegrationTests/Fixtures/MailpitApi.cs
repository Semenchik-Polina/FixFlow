using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>
/// Reads the mail the test Mailpit received, through its HTTP API. Read only by recipient: tests run in parallel
/// against the one Mailpit, each with its own addresses.
/// </summary>
public sealed class MailpitApi(Uri baseAddress) : IDisposable
{
    private static readonly TimeSpan _arrivalTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(100);

    private readonly HttpClient _http = new() { BaseAddress = baseAddress };

    /// <summary>
    /// The one message to <paramref name="recipient"/>. Mailpit stores a message just after it accepts it, so this
    /// waits a few seconds for it to show up.
    /// </summary>
    public async Task<MailpitMessage> SingleToAsync(string recipient, CancellationToken cancellationToken)
    {
        var query = Uri.EscapeDataString($"to:\"{recipient}\"");
        var waited = Stopwatch.StartNew();

        while (true)
        {
            var found = await _http.GetFromJsonAsync<SearchResult>(
                new Uri($"api/v1/search?query={query}", UriKind.Relative),
                JsonSerializerOptions.Web,
                cancellationToken);

            if (found!.Messages.Count > 0 || waited.Elapsed > _arrivalTimeout)
            {
                var summary = Assert.Single(found.Messages);
                return (await _http.GetFromJsonAsync<MailpitMessage>(
                    new Uri($"api/v1/message/{summary.Id}", UriKind.Relative),
                    JsonSerializerOptions.Web,
                    cancellationToken))!;
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }
    }

    /// <summary>A header of a message, as the SMTP server received it.</summary>
    public async Task<string> HeaderAsync(MailpitMessage message, string name, CancellationToken cancellationToken)
    {
        var headers = await _http.GetFromJsonAsync<Dictionary<string, string[]>>(
            new Uri($"api/v1/message/{message.Id}/headers", UriKind.Relative),
            JsonSerializerOptions.Web,
            cancellationToken);

        return Assert.Single(headers![name]);
    }

    public void Dispose() => _http.Dispose();

    private sealed record SearchResult(IReadOnlyList<MessageSummary> Messages);

    private sealed record MessageSummary(string Id);
}

public sealed record MailpitMessage(
    string Id, MailpitAddress From, IReadOnlyList<MailpitAddress> To, string Subject, string Text, string Html);

public sealed record MailpitAddress(string Name, string Address);
