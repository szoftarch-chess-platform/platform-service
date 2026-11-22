namespace PlatformService.UnitTests.Fakes;

/// <summary>A clock in UTC that stands still until the test moves it on.</summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan time) => _now += time;
}
