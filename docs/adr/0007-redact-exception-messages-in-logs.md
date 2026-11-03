# ADR 0007: Log post-commit failures with stack traces but without exception messages

- **Status:** Accepted
- **Date:** 2026-10-22
- **Amends:** ADR 0005 (decision 3), S26 (`ARCHITECTURE.md` §17), `src/CLAUDE.md` §7

## Context

ADR 0005 says a failed post-commit handler is "logged with the event type and IDs only". The first dispatcher logged
the whole exception instead, and exception messages can hold personal data: an SMTP error quotes the recipient
("550 5.1.1 <jane.doe@…>: Recipient address rejected"). Log masking (`Serilog.Enrichers.Sensitive`) works on log
properties and the message template. It doesn't reach exceptions.

Logging the exception type alone keeps addresses out, but it's too little to find the cause: no stack trace, no inner
exceptions. A message can't be judged safe by its exception's type either. The thrower writes it as free text, and
framework messages that look harmless carry data too: a `KeyNotFoundException` quotes the key, and a `FormatException`
quotes the input it couldn't parse.

## Decision

1. A failed post-commit handler is logged with its exception's **types and stack traces** (inner exceptions included,
   in the layout of `Exception.ToString()`) and **without messages**. Nothing else of the exception is read, so details
   a type adds to its own `ToString()` stay out too.
2. **Opt-in:** an exception that implements `ILogSafeMessage` keeps its message. Its message must be built from IDs,
   codes and fixed text only. Its inner exceptions are judged on their own.
3. **Safe fields** of well-known exceptions are kept: names, codes and numbers, never free text.
   - `ArgumentException.ParamName`
   - `SocketException.SocketErrorCode`
   - `HttpRequestException.StatusCode` and `HttpRequestError`
   - `PostgresException.SqlState` and `ConstraintName`
4. In the **Development** environment, exceptions are logged as thrown, messages included. There is no setting for it,
   only the environment. Testing, the `full` Compose profile (Production, plan P18) and anything else redact.
5. Scope: the post-commit dispatcher (`DomainEventDispatcher`), where side effects such as email fail.
   Extended by T1.4 to failures a handler catches (plan P30): it logs `IExceptionRedactor.ForLog(exception)`, which
   applies these rules.

## Consequences

- Logs show where and in which call a side effect failed, but not the text of the reason. To log the reason, the code
  that knows which details are safe rethrows with an `ILogSafeMessage` exception. The T1.4 email sender does this with
  the SMTP status code.
- Development logs may contain whatever data a developer enters locally. They stay in the local Seq, and local data is
  test data (S21).
- **Follow-up:** unhandled request exceptions (500s) and startup failures still log full exceptions, messages
  included. Extending the redaction to them is a separate task.
