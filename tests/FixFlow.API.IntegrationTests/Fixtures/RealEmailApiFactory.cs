using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using NodaTime.Testing;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>
/// An API host that keeps the app's SMTP sender (<see cref="FixFlowApiFactory.WithRealEmail"/>), so its mail reaches
/// an SMTP server and <see cref="FixFlowApiFactory.Emails"/> stays empty. It shares the run's containers and starts on
/// first use.
/// </summary>
public sealed class RealEmailApiFactory : FixFlowApiFactory
{
    private readonly SmtpEndpoint _smtp;

    internal RealEmailApiFactory(TestInfrastructure infrastructure, SmtpEndpoint smtp, FakeClock clock)
        : base(infrastructure, ownsInfrastructure: false, clock)
    {
        _smtp = smtp;
    }

    private protected override bool CapturesEmail => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Mailpit speaks plain SMTP; the sender address comes from appsettings.json
        builder
            .UseSetting("Smtp:Host", _smtp.Host)
            .UseSetting("Smtp:Port", _smtp.Port.ToString(CultureInfo.InvariantCulture))
            .UseSetting("Smtp:Security", "None");
    }
}
