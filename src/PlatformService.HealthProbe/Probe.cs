using System.Diagnostics.CodeAnalysis;

namespace PlatformService.HealthProbe;

/// <summary>
/// Asks a health endpoint of the service for the health check of its container (PS-ADR-9).
/// </summary>
internal static class Probe
{
    /// <summary>The exit code that Docker takes as healthy.</summary>
    public const int Healthy = 0;

    /// <summary>The exit code that Docker takes as unhealthy. Docker defines no other code and reserves 2.</summary>
    public const int Unhealthy = 1;

    /// <summary>
    /// Shorter than the timeout of the health check in the compose of the infra repo, so the probe can still write
    /// why it failed, which Docker keeps with the result of the check.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);

    /// <summary>Reads the URL of the health endpoint from the arguments, which have to be one http or https URL.</summary>
    /// <remarks>On Linux an absolute path such as /health/ready is a file URI too, which HttpClient cannot send.</remarks>
    public static bool TryGetUrl(string[] args, [NotNullWhen(true)] out Uri? url)
    {
        if (args.Length == 1
            && Uri.TryCreate(args[0], UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
        {
            url = parsed;
            return true;
        }

        url = null;
        return false;
    }

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
