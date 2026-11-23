namespace PlatformService.IntegrationTests.Messaging;

/// <summary>Waits for something that the consumer does in the background.</summary>
internal static class Eventually
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan _interval = TimeSpan.FromMilliseconds(50);

    /// <summary>Asks the probe again until it returns a value, and fails after 15 seconds.</summary>
    /// <param name="what">What the test waits for, for the message of the failure.</param>
    public static async Task<T> GetAsync<T>(Func<Task<T?>> probe, string what)
        where T : class
    {
        var deadline = DateTimeOffset.UtcNow + _timeout;
        while (true)
        {
            if (await probe() is { } value)
            {
                return value;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                Assert.Fail($"Waited {_timeout.TotalSeconds} seconds for {what}.");
            }

            await Task.Delay(_interval, TestContext.Current.CancellationToken);
        }
    }
}
