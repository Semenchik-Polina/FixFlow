using FixFlow.Application.Email;

namespace FixFlow.Application.Identity.ForgotPassword;

/// <summary>The email with a reset link (S4). Its wording lives here (D27).</summary>
public static class PasswordResetEmail
{
    public const string Subject = "Reset your FixFlow password";

    /// <param name="to">The account's email, as stored.</param>
    /// <param name="firstName">The account's first name, for the greeting.</param>
    /// <param name="link">The reset page, with the code in its fragment (<see cref="ISpaLinks.ResetPassword"/>).</param>
    public static EmailMessage Create(string to, string firstName, Uri link) =>
        new EmailLayout()
            .Paragraph($"Hi {firstName},")
            .Paragraph(
                "We received a request to reset the password of your FixFlow account. Choose a new one with the link "
                + "below. The link works once, within an hour.")
            .Link("Choose a new password", link)
            .Paragraph("If you didn't ask for this, you can ignore this email: your password stays as it is.")
            .ToMessage(to, Subject);
}
