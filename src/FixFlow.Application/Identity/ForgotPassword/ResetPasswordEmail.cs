using System.Globalization;
using FixFlow.Application.Email;
using FixFlow.Application.Identity.ResetPassword;
using NodaTime;

namespace FixFlow.Application.Identity.ForgotPassword;

/// <summary>The email with a reset link (S4). Its wording lives here (D27).</summary>
public static class ResetPasswordEmail
{
    public const string Subject = "Reset your FixFlow password";

    /// <param name="to">The account's email, as stored.</param>
    /// <param name="firstName">The account's first name, for the greeting.</param>
    /// <param name="link">The reset page, with the code in its fragment (<see cref="ISpaLinks.ResetPassword"/>).</param>
    /// <param name="lifespan">
    /// How long the link works (<see cref="ResetPasswordCode.Lifespan"/>), which the email states: a whole number of
    /// minutes.
    /// </param>
    public static EmailMessage Create(string to, string firstName, Uri link, Duration lifespan) =>
        new EmailLayout()
            .Paragraph($"Hi {firstName},")
            .Paragraph(
                "We received a request to reset the password of your FixFlow account. Choose a new one with the link "
                + $"below. The link works once, within {InWords(lifespan)}.")
            .Link("Choose a new password", link)
            .Paragraph("If you didn't ask for this, you can ignore this email: your password stays as it is.")
            .ToMessage(to, Subject);

    /// <summary>"an hour", "2 hours", "30 minutes": in hours when it is whole hours.</summary>
    private static string InWords(Duration lifespan)
    {
        var minutes = lifespan.TotalMinutes;
        if (minutes < 1 || minutes % 1 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lifespan), lifespan, "A link's lifespan is whole minutes.");
        }

        return minutes switch
        {
            1 => "a minute",
            60 => "an hour",
            _ when minutes % 60 == 0 => string.Create(CultureInfo.InvariantCulture, $"{minutes / 60} hours"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{minutes} minutes"),
        };
    }
}
