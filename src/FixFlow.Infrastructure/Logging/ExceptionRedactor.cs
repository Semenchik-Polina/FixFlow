using FixFlow.Application.Errors;
using Microsoft.Extensions.Options;

namespace FixFlow.Infrastructure.Logging;

/// <summary><see cref="RedactedException"/> unless <see cref="ExceptionLoggingOptions.KeepMessages"/> is on.</summary>
internal sealed class ExceptionRedactor(IOptions<ExceptionLoggingOptions> options) : IExceptionRedactor
{
    public Exception ForLog(Exception exception) =>
        options.Value.KeepMessages ? exception : RedactedException.For(exception);
}
