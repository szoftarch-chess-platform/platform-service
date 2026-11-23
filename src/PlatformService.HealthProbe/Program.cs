using PlatformService.HealthProbe;

// The compose of the infra repo runs this program as the health check of the container with the URL of the
// readiness endpoint, for example ./health-probe/PlatformService.HealthProbe http://127.0.0.1:8080/health/ready
if (args.Length != 1 || !Uri.TryCreate(args[0], UriKind.Absolute, out var url))
{
    await Console.Error.WriteLineAsync("Give the URL of a health endpoint as the only argument.");
    return Probe.Unhealthy;
}

using var client = new HttpClient { Timeout = Probe.Timeout };
return await Probe.CheckAsync(client, url, Console.Error, CancellationToken.None);
