using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PlatformService.Infrastructure.Identity;
using PlatformService.Infrastructure.Persistence;

namespace PlatformService.Infrastructure;

public static class DependencyInjection
{
    /// <summary>The letters of the English alphabet, the digits, the underscore and the hyphen of ADR-4.</summary>
    private const string AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-";

    /// <summary>
    /// Registers the PostgreSQL database through EF Core, its readiness health check and the accounts of Identity.
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
            options.UsePlatformDatabase(database.ConnectionString);
        });

        services.AddHealthChecks()
            .AddDbContextCheck<PlatformDbContext>(tags: [HealthCheckTags.Ready]);

        // Only the user management of Identity, without its cookies, tokens and endpoints (PS-ADR-11).
        services.AddIdentityCore<PlatformUser>(ConfigureIdentity)
            .AddEntityFrameworkStores<PlatformDbContext>();

        return services;
    }

    /// <summary>
    /// The rules of the accounts of ADR-4. The defaults of Identity differ from them, so every option is set.
    /// </summary>
    private static void ConfigureIdentity(IdentityOptions options)
    {
        // The validators of PS-ADR-6 check the length of the user name.
        options.User.AllowedUserNameCharacters = AllowedUserNameCharacters;
        options.User.RequireUniqueEmail = true;

        // 8 characters without composition rules. Identity counts UTF-16 code units, which are never fewer than
        // the code points that the validators of PS-ADR-6 count, so it accepts every password that they accept.
        options.Password.RequiredLength = 8;
        options.Password.RequiredUniqueChars = 1;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        // The gateway limits the sign-in attempts of an IP address instead (ADR-3).
        options.Lockout.AllowedForNewUsers = false;
    }
}
