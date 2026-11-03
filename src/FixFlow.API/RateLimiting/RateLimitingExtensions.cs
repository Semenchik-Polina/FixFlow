using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FixFlow.API.RateLimiting;

public static class RateLimitingExtensions
{
    /// <summary>The client part of the partition key for a request without an address.</summary>
    public const string UnknownClient = "unknown";

    /// <summary>
    /// The <see cref="RateLimitPolicies.Auth"/> policy (S6, plan P28): a fixed window with no queue, one budget per
    /// endpoint and client address (<see cref="ClientAddressKey"/>). A rejected request gets a 429 Problem Details
    /// with <c>Retry-After</c>. The limits are bound from <see cref="AuthRateLimitOptions.SectionName"/> and checked
    /// at startup.
    /// </summary>
    public static IServiceCollection AddFixFlowRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<AuthRateLimitOptions>()
            .BindConfiguration(AuthRateLimitOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;

            options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
            {
                var limits = httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
                var route = (httpContext.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
                var client = ClientAddressKey(httpContext.Connection.RemoteIpAddress);

                return RateLimitPartition.GetFixedWindowLimiter((route, client), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.PermitLimit,
                    Window = limits.Window,
                    QueueLimit = 0,
                });
            });
        });

        return services;
    }

    /// <summary>
    /// The client part of a partition key: an IPv4 address as it is (an IPv4-mapped IPv6 address counts as IPv4), or
    /// the /64 prefix of an IPv6 address. A client usually controls a whole /64, and would otherwise get a fresh budget
    /// from each address in it. Requests without an address share <see cref="UnknownClient"/>.
    /// </summary>
    public static string ClientAddressKey(IPAddress? address)
    {
        if (address is null)
        {
            return UnknownClient;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4().ToString();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        Span<byte> bytes = stackalloc byte[16];
        address.TryWriteBytes(bytes, out _);
        bytes[8..].Clear();
        return $"{new IPAddress(bytes)}/64";
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;

        // A fixed window's rejection carries the window's length, which the wait never exceeds
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(NumberFormatInfo.InvariantInfo);
        }

        // False only when the client accepts no JSON: the status code alone still answers it
        await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = "Too many attempts. Try again later.",
                },
            });
    }
}
