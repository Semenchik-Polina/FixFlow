using System.Text;

namespace FixFlow.Application.Email;

/// <summary>One email to one recipient. <see cref="HtmlBody"/> is optional; <see cref="TextBody"/> is always sent.</summary>
public sealed record EmailMessage(string To, string Subject, string TextBody, string? HtmlBody = null)
{
    // A logged message prints neither the address nor the bodies, which can carry a link with a token (S26)
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Subject = ").Append(Subject);
        return true;
    }
}
