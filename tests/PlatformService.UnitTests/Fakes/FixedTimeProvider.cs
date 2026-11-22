namespace PlatformService.UnitTests.Fakes;

/// <summary>A clock that always shows the same time in UTC.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
