using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PlatformService.Application.Abstractions;
using PlatformService.Application.Auth;
using PlatformService.Infrastructure.Identity;
using PlatformService.Infrastructure.Messaging;
using PlatformService.Infrastructure.Persistence;
using PlatformService.Infrastructure.Tokens;

namespace PlatformService.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the PostgreSQL database through EF Core, its readiness health check, the accounts of Identity,
    /// the sessions with the cleanup of the expired ones, the finished games with their consumer of RabbitMQ and the
    /// signing of the access tokens.
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

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddHostedService<ExpiredSessionCleanup>();
        services.AddScoped<IGameRepository, GameRepository>();

        // Only the user management of Identity, without its cookies, tokens and endpoints. Its store does not save
        // on its own, so the user is saved in the commit of the use case (PS-ADR-11).
        services.AddIdentityCore<PlatformUser>(ConfigureIdentity)
            .AddUserStore<PlatformUserStore>();
        services.AddScoped<IUserAccounts, IdentityUserAccounts>();

        AddAccessTokens(services, configuration);
        AddGameFinishedConsumer(services, configuration);

        return services;
    }

    /// <summary>
    /// The consumer of <c>game.finished.v1</c> with the connection of <c>ConnectionStrings:RabbitMq</c> (PS-ADR-14).
    /// The readiness check does not include the broker.
    /// </summary>
    private static void AddGameFinishedConsumer(IServiceCollection services, IConfiguration configuration)
    {
        // Read lazily and validated at startup, like the database settings above.
        const string setting = $"ConnectionStrings:{RabbitMqOptions.ConnectionStringName}";
        services.AddOptions<RabbitMqOptions>()
            .Configure(options => options.ConnectionString =
                configuration.GetConnectionString(RabbitMqOptions.ConnectionStringName) ?? string.Empty)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                $"The connection string '{setting}' is not set. Set it with the "
                + $"ConnectionStrings__{RabbitMqOptions.ConnectionStringName} environment variable "
                + "or locally with dotnet user-secrets.")
            .Validate(
                options => string.IsNullOrWhiteSpace(options.ConnectionString) || RabbitMqOptions.IsAmqpUri(options.ConnectionString),
                $"The connection string '{setting}' has to be an AMQP URI, such as amqp://user:password@host:5672/%2f.")
            .ValidateOnStart();

        services.AddSingleton<GameFinishedHandler>();
        services.AddHostedService<GameFinishedConsumer>();
    }

    /// <summary>The signing of the access tokens with the key of <c>Jwt:SigningKeyPath</c> (PS-ADR-12).</summary>
    private static void AddAccessTokens(IServiceCollection services, IConfiguration configuration)
    {
        // Bound lazily and validated at startup, like the database settings above.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        // The key is read once and kept in memory.
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
    }

    /// <summary>
    /// The rules of the accounts of ADR-4. The defaults of Identity differ from them, so every option is set.
    /// </summary>
    private static void ConfigureIdentity(IdentityOptions options)
    {
        // The validators of PS-ADR-6 check the length of the user name.
        options.User.AllowedUserNameCharacters = AccountRules.AllowedUserNameCharacters;
        options.User.RequireUniqueEmail = true;

        // The minimum length of ADR-4 without composition rules. Identity counts UTF-16 code units, which are never
        // fewer than the code points that the validators of PS-ADR-6 count, so it accepts every password that they accept.
        options.Password.RequiredLength = AccountRules.PasswordMinLength;
        options.Password.RequiredUniqueChars = 1;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        // The gateway limits the sign-in attempts of an IP address instead (ADR-3).
        options.Lockout.AllowedForNewUsers = false;
    }
}
