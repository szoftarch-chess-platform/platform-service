namespace PlatformService.IntegrationTests;

/// <summary>
/// The system clock moved on by the time that a test adds, for example to reach the end of the grace window. It
/// never goes back, so the rows of the other tests in the shared database are never older than the real time.
/// </summary>
public sealed class TestClock : TimeProvider
{
    private long _offsetTicks;

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

    public void Advance(TimeSpan time)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(time, TimeSpan.Zero);
        Interlocked.Add(ref _offsetTicks, time.Ticks);
    }

    /// <summary>Goes back to the real time, which a test class calls before each of its tests.</summary>
    public void Reset() => Interlocked.Exchange(ref _offsetTicks, 0);
}
