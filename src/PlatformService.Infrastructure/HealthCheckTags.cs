namespace PlatformService.Infrastructure;

/// <summary>Tags of the health checks registered by <see cref="DependencyInjection.AddInfrastructure"/>.</summary>
public static class HealthCheckTags
{
    /// <summary>Checks that must pass before the service can take traffic. Currently this is the database check.</summary>
    public const string Ready = "ready";
}
