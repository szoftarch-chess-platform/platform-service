namespace PlatformService.HealthProbe;

/// <summary>
/// Asks a health endpoint of the service for the health check of its container (PS-ADR-9).
/// </summary>
internal static class Probe
{
    /// <summary>The exit code that Docker takes as healthy.</summary>
    public const int Healthy = 0;

    /// <summary>The exit code that Docker takes as unhealthy. Docker reserves every other code.</summary>
    public const int Unhealthy = 1;

    /// <summary>
    /// Shorter than the timeout of the health check in the compose of the infra repo, so the probe can still write
    /// why it failed, which Docker keeps with the result of the check.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Returns <see cref="Healthy"/> when <paramref name="url"/> answers with a success status code, and
    /// <see cref="Unhealthy"/> for any other answer or for no answer, with the reason written to
    /// <paramref name="error"/>.
    /// </summary>
    public static async Task<int> CheckAsync(
        HttpClient client, Uri url, TextWriter error, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return Healthy;
            }

            await error.WriteLineAsync($"{url} answered {(int)response.StatusCode}.");
            return Unhealthy;
        }
        catch (HttpRequestException exception)
        {
            await error.WriteLineAsync($"{url} did not answer. {exception.Message}");
            return Unhealthy;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation.
            await error.WriteLineAsync($"{url} did not answer within {client.Timeout.TotalSeconds} seconds.");
            return Unhealthy;
        }
    }
}
