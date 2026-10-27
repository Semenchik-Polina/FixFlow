using System.ComponentModel.DataAnnotations;
using MailKit.Security;

namespace FixFlow.Infrastructure.Email;

/// <summary>
/// The SMTP server mail goes through, from <c>Smtp</c>. <see cref="Host"/> has no safe default, so the settings are
/// checked when the first message is sent, not at startup: the build-time OpenAPI run needs none.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    [Required]
    public string Host { get; set; } = "";

    /// <summary>587, the submission port, where servers expect <see cref="SecureSocketOptions.StartTls"/>.</summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    /// <summary>
    /// <see cref="SecureSocketOptions.StartTls"/> by default: TLS is required, never just tried. Mailpit locally
    /// takes <see cref="SecureSocketOptions.None"/>.
    /// </summary>
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.StartTls;

    /// <summary>Optional; set together with <see cref="Password"/> when the server requires authentication.</summary>
    public string? UserName { get; set; }

    /// <summary>A secret: user-secrets locally, an environment variable elsewhere, never a committed file.</summary>
    public string? Password { get; set; }

    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = "";

    [Required]
    public string FromName { get; set; } = "FixFlow";

    /// <summary>How long one SMTP step (connecting, each command) may take.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
