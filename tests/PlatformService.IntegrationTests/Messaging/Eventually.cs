namespace PlatformService.IntegrationTests.Messaging;

/// <summary>Waits for something that the consumer does in the background.</summary>
internal static class Eventually
{
    /// <summary>How long a test waits for the broker or the consumer.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>How long a test waits before it tries again.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);

    /// <summary>Asks the probe again until it returns a value, and fails after <see cref="Timeout"/>.</summary>
    /// <param name="what">What the test waits for, for the message of the failure.</param>
    public static async Task<T> GetAsync<T>(Func<Task<T?>> probe, string what)
        where T : class
    {
        var deadline = DateTimeOffset.UtcNow + Timeout;
        while (true)
        {
            if (await probe() is { } value)
            {
                return value;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                Assert.Fail($"Waited {Timeout.TotalSeconds} seconds for {what}.");
            }

            await Task.Delay(Interval, TestContext.Current.CancellationToken);
        }
    }
}
