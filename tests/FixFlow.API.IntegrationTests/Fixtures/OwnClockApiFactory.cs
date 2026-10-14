using NodaTime;
using NodaTime.Testing;

namespace FixFlow.API.IntegrationTests.Fixtures;

/// <summary>
/// An API host with a clock only this test controls (<see cref="FixFlowApiFactory.WithOwnClock"/>). It shares the
/// run's containers and starts on first use.
/// </summary>
public sealed class OwnClockApiFactory : FixFlowApiFactory
{
    internal OwnClockApiFactory(TestInfrastructure infrastructure, FakeClock clock)
        : base(infrastructure, ownsInfrastructure: false, clock)
    {
        Clock = clock;
    }

    /// <summary>The app's clock; <see cref="FakeClock.Advance(Duration)"/> moves time for this host only.</summary>
    public new FakeClock Clock { get; }
}
