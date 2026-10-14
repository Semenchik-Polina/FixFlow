namespace FixFlow.Application.Email;

/// <summary>One email to one recipient. <see cref="HtmlBody"/> is optional; <see cref="TextBody"/> is always sent.</summary>
public sealed record EmailMessage(string To, string Subject, string TextBody, string? HtmlBody = null);
