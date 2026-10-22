using System.Globalization;
using System.Net.Sockets;
using System.Text;
using FixFlow.Application.Errors;
using Npgsql;

namespace FixFlow.Infrastructure.Logging;

/// <summary>
/// An exception as logs may show it (ADR 0007): the type, safe fields and stack trace of the original and of each
/// inner exception, laid out like <see cref="Exception.ToString"/>, but no messages. A message can hold personal data
/// (an SMTP error quoting the address), and log masking doesn't cover exceptions (S26). Only an
/// <see cref="ILogSafeMessage"/> exception keeps its message. Nothing else of the original is read: types that add
/// details to their own <c>ToString</c> (a file name, a row's values) don't leak them either.
/// </summary>
internal sealed class RedactedException : Exception
{
    private const string MessageRemoved = "[message removed]";
    private const string InnerPrefix = " ---> ";
    private const string EndOfInnerStack = "   --- End of inner exception stack trace ---";

    private readonly string _text;

    private RedactedException(string message, string text)
        : base(message) => _text = text;

    /// <summary>The original's trace is part of <see cref="ToString"/>; this exception was never thrown.</summary>
    public override string? StackTrace => null;

    public static RedactedException For(Exception exception)
    {
        var text = new StringBuilder();
        Describe(exception, text);
        return new RedactedException(Header(exception), text.ToString());
    }

    public override string ToString() => _text;

    private static void Describe(Exception exception, StringBuilder text)
    {
        text.Append(Header(exception));

        if (exception.InnerException is { } inner)
        {
            text.AppendLine().Append(InnerPrefix);
            Describe(inner, text);
            text.AppendLine().Append(EndOfInnerStack);
        }

        if (exception.StackTrace is { } stackTrace)
        {
            text.AppendLine().Append(stackTrace);
        }

        // Like AggregateException.ToString: the first inner exception is above, the others follow
        if (exception is AggregateException aggregate)
        {
            for (var i = 0; i < aggregate.InnerExceptions.Count; i++)
            {
                if (aggregate.InnerExceptions[i] == aggregate.InnerException)
                {
                    continue;
                }

                text.AppendLine().Append(InnerPrefix).Append(CultureInfo.InvariantCulture, $"(Inner Exception #{i}) ");
                Describe(aggregate.InnerExceptions[i], text);
                text.AppendLine("<---");
            }
        }
    }

    private static string Header(Exception exception)
    {
        var fields = SafeFields(exception)
            .Where(field => field.Value is not null)
            .Select(field => $"{field.Name}={Convert.ToString(field.Value, CultureInfo.InvariantCulture)}")
            .ToList();
        var details = fields.Count > 0 ? $" [{string.Join(", ", fields)}]" : "";
        var message = exception is ILogSafeMessage ? exception.Message : MessageRemoved;

        return $"{exception.GetType().FullName}{details}: {message}";
    }

    /// <summary>Names, codes and numbers, never free text: they explain a failure without carrying data.</summary>
    private static IEnumerable<(string Name, object? Value)> SafeFields(Exception exception) => exception switch
    {
        ArgumentException argument => [(nameof(argument.ParamName), argument.ParamName)],
        SocketException socket => [(nameof(socket.SocketErrorCode), socket.SocketErrorCode)],
        HttpRequestException http =>
            [(nameof(http.StatusCode), http.StatusCode), (nameof(http.HttpRequestError), http.HttpRequestError)],
        PostgresException postgres =>
        [
            (nameof(postgres.SqlState), postgres.SqlState),
            (nameof(postgres.ConstraintName), postgres.ConstraintName),
        ],
        _ => [],
    };
}
