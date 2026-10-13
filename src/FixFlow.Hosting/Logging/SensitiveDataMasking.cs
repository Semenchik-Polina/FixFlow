using System.Text.RegularExpressions;
using Serilog.Enrichers.Sensitive;

namespace FixFlow.Hosting.Logging;

/// <summary>
/// Masks sensitive values in every log event (S26). A safety net, not the rule: log IDs only (src/CLAUDE.md §7).
/// Not covered: exception messages and stack traces. EF Core's EnableSensitiveDataLogging must stay off.
/// </summary>
public static class SensitiveDataMasking
{
    // Exact names, matched case-insensitively at any depth (destructured objects, lists, dictionaries). The whole
    // value is replaced, whatever its type. A new sensitive property name must be added here.
    // No wildcard rules: in Serilog.Enrichers.Sensitive 2.1.0 they mask only string values, an event whose property
    // matches two rules goes out unmasked, and their match cache isn't thread-safe.
    private static readonly string[] _propertyNames =
    [
        "Password", "CurrentPassword", "NewPassword", "ConfirmPassword", "PasswordHash",
        "Token", "ResetToken", "InviteToken", "PasswordResetToken", "EmailConfirmationToken",
        "AccessToken", "RefreshToken", "SecurityStamp", "ApiKey", "Secret", "ClientSecret",
        "Email", "NormalizedEmail", "UserName", "NormalizedUserName", "PhoneNumber", "Phone",
        // Not plain "Address": ASP.NET Core logs its listening URL under that name
        "FirstName", "LastName", "FullName", "StreetAddress", "PostalAddress",
        "Cookie", "Authorization", "ConnectionString", "Sas", "SasUrl", "SasUri",
    ];

    public static void Configure(SensitiveDataEnricherOptions options)
    {
        options.MaskProperties = [.. _propertyNames.Select(MaskProperty.WithDefaults)];

        // These scan every string value and the message template, so an email address or a SAS signature is masked
        // whatever its property name. The first three are the library's defaults.
        options.MaskingOperators =
        [
            new EmailAddressMaskingOperator(),
            new IbanMaskingOperator(),
            new CreditCardMaskingOperator(),
            new SasSignatureMaskingOperator(),
        ];
    }

    // A SAS URL is a bearer credential: mask the value of its signature parameter
    private sealed class SasSignatureMaskingOperator()
        : RegexMaskingOperator(@"(?<=\bsig=)[^&\s""]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
