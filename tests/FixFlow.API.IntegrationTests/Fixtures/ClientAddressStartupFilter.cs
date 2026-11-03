using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>
/// Sets the connection's client address from <see cref="WebApplicationFactoryExtensions.ClientAddressHeader"/>, ahead
/// of the app's whole pipeline. A request without the header keeps TestServer's null address.
/// </summary>
internal sealed class ClientAddressStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            var header = context.Request.Headers[WebApplicationFactoryExtensions.ClientAddressHeader].ToString();
            if (IPAddress.TryParse(header, out var address))
            {
                context.Connection.RemoteIpAddress = address;
            }

            return nextMiddleware(context);
        });

        next(app);
    };
}
