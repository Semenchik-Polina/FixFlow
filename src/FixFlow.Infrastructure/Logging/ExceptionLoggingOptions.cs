namespace FixFlow.Infrastructure.Logging;

/// <summary>How exceptions are logged when a post-commit handler fails (ADR 0007).</summary>
public sealed class ExceptionLoggingOptions
{
    /// <summary>
    /// Logs exceptions as thrown, messages included. Off by default: messages are removed, since they can hold
    /// personal data. The API turns it on in Development only, where the data is test data.
    /// </summary>
    public bool KeepMessages { get; set; }
}
