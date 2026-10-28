using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FixFlow.Application.Identity.ResetPassword;

/// <summary>
/// The one opaque value a reset link carries: <c>{userId:N}.{token}</c>, the account's id and Identity's token. The
/// SPA posts it back as it is, so it never handles an email address. URL-safe: hex digits, a dot, and the token's
/// base64url.
/// </summary>
public static class PasswordResetCode
{
    private const char Separator = '.';

    public static string Format(Guid userId, string token) =>
        string.Create(CultureInfo.InvariantCulture, $"{userId:N}{Separator}{token}");

    /// <summary>
    /// Splits a code into its user id and token. False for anything <see cref="Format"/> can't have made; the token
    /// itself is checked only by Identity.
    /// </summary>
    public static bool TryParse(string? code, out Guid userId, [NotNullWhen(true)] out string? token)
    {
        var separator = code?.IndexOf(Separator, StringComparison.Ordinal) ?? -1;
        if (separator < 0
            || separator == code!.Length - 1
            || !Guid.TryParseExact(code.AsSpan(0, separator), "N", out userId))
        {
            userId = Guid.Empty;
            token = null;
            return false;
        }

        token = code[(separator + 1)..];
        return true;
    }
}
