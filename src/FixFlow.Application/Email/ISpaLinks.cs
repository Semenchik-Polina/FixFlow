namespace FixFlow.Application.Email;

/// <summary>
/// Links to the SPA's pages, for emails. They are built from configuration (<c>Spa:PublicUrl</c>), never from the
/// request's <c>Host</c>: a link built from the request would let anyone who sends a forged <c>Host</c> header get a
/// victim's reset link pointing at their own site (password-reset poisoning). Tokens travel in the fragment, so they
/// never reach a server's logs (A11).
/// </summary>
public interface ISpaLinks
{
    /// <summary><c>{PublicUrl}/reset-password#token={code}</c>.</summary>
    Uri ResetPassword(string code);
}
