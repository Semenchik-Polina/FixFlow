using FixFlow.Application.Email;
using Microsoft.Extensions.Options;

namespace FixFlow.API.Spa;

/// <summary>
/// Links from <see cref="SpaLinkOptions.PublicUrl"/>, never from the request (see <see cref="ISpaLinks"/>).
/// </summary>
internal sealed class SpaLinks(IOptions<SpaLinkOptions> options) : ISpaLinks
{
    public Uri ResetPassword(string code) => Page($"reset-password#token={Uri.EscapeDataString(code)}");

    private Uri Page(string relative) =>
        new($"{options.Value.PublicUrl!.AbsoluteUri.TrimEnd('/')}/{relative}", UriKind.Absolute);
}
