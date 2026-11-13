using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PlatformService.Infrastructure.Persistence;

namespace PlatformService.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the PostgreSQL database through EF Core and its readiness health check.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Read lazily and validated at startup, so configuration added after this call still counts.
        // WebApplicationFactory adds the test configuration this way.
        services.AddOptions<DatabaseOptions>()
            .Configure(options => options.ConnectionString =
                configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                $"The connection string 'ConnectionStrings:{DatabaseOptions.ConnectionStringName}' is not set. "
                + $"Set it with the ConnectionStrings__{DatabaseOptions.ConnectionStringName} environment variable "
                + "or locally with dotnet user-secrets.")
            .ValidateOnStart();

        // Connecting is deferred to the first query, so the app starts even when the database is down.
        services.AddDbContext<PlatformDbContext>((serviceProvider, options) =>
        {
            var database = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(database.ConnectionString)
                .UseSnakeCaseNamingConvention();
        });

        services.AddHealthChecks()
            .AddDbContextCheck<PlatformDbContext>(tags: [HealthCheckTags.Ready]);

        return services;
    }
}
