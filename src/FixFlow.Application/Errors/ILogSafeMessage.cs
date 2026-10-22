namespace FixFlow.Application.Errors;

/// <summary>
/// Marks an exception whose message is safe to log: built from IDs, codes and fixed text only, never from user input
/// or personal data (S26). Logs keep its message; every other exception's message is removed outside Development
/// (ADR 0007). Its inner exceptions are judged on their own.
/// </summary>
public interface ILogSafeMessage;
