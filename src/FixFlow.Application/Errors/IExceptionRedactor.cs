namespace FixFlow.Application.Errors;

/// <summary>
/// Prepares a caught exception for a log (ADR 0007). Its message can hold personal data (an SMTP error quoting the
/// address), and log masking doesn't cover exceptions (S26). Code that catches an exception and logs it instead of
/// letting it fail the request passes it through here first.
/// </summary>
public interface IExceptionRedactor
{
    /// <summary>
    /// Outside Development, the exception's types, safe fields and stack traces, without messages; only an
    /// <see cref="ILogSafeMessage"/> exception keeps its own. In Development, the exception as thrown.
    /// </summary>
    Exception ForLog(Exception exception);
}
