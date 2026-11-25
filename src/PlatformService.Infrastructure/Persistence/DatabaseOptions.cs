namespace PlatformService.Infrastructure.Persistence;

/// <summary>Database settings, read from <c>ConnectionStrings:Platform</c>.</summary>
internal sealed class DatabaseOptions
{
    public const string ConnectionStringName = "Platform";

    public string ConnectionString { get; set; } = string.Empty;
}
