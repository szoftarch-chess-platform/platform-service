using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PlatformService.Infrastructure.Messaging;
using PlatformService.Infrastructure.Persistence;
using PlatformService.IntegrationTests.ErrorHandling;

namespace PlatformService.IntegrationTests;

/// <summary>
/// Hosts the real app in memory against the database of <see cref="PostgresFixture"/> with the key of
/// <see cref="SigningKeyFixture"/> and the time of <see cref="Clock"/>, plus the test-only controllers of this project.
/// It runs without the consumer of RabbitMQ, which <see cref="WithConsumer"/> adds.
/// </summary>
public sealed class PlatformApiFactory(PostgresFixture postgres, SigningKeyFixture signingKey) : WebApplicationFactory<Program>
{
    /// <summary>The time of the app, which follows the real time until a test moves it on.</summary>
    public TestClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Testing is not Development, so errors look as in production and no user secrets are loaded.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Platform", postgres.ConnectionString);
        builder.UseSetting("Jwt:SigningKeyPath", signingKey.KeyPath);

        // The consumer is removed below, so this URI is only validated and never used.
        builder.UseSetting($"ConnectionStrings:{RabbitMqOptions.ConnectionStringName}", "amqp://localhost/%2f");

        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(ErrorTestController).Assembly);
            services.AddValidatorsFromAssemblyContaining<ErrorTestController>();
            services.AddSingleton<TimeProvider>(Clock);

            // The tests share one database, and the clock of a test can be ahead, so the cleanup of one host could
            // delete the rows of other tests. ExpiredSessionCleanupTests call the cleanup directly instead.
            services.Remove(services.Single(service => service.ImplementationType == typeof(ExpiredSessionCleanup)));

            // The queue of the consumer belongs to a virtual host of RabbitMqFixture, which only the tests of the
            // consumer create.
            services.Remove(services.Single(service => service.ImplementationType == typeof(GameFinishedConsumer)));
        });
    }

    /// <summary>
    /// The app with the consumer of PS-ADR-14 on the broker of <paramref name="rabbitMqUri"/>, which connects again and
    /// tries the binding again after 100 milliseconds instead of 5 seconds.
    /// </summary>
    /// <param name="services">Replaces services of the app, for example the use case with one that fails.</param>
    public WebApplicationFactory<Program> WithConsumer(string rabbitMqUri, Action<IServiceCollection>? services = null) =>
        WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"ConnectionStrings:{RabbitMqOptions.ConnectionStringName}", rabbitMqUri);
            builder.ConfigureTestServices(testServices =>
            {
                testServices.AddHostedService<GameFinishedConsumer>();
                testServices.Configure<RabbitMqOptions>(options => options.RetryDelay = TimeSpan.FromMilliseconds(100));
                services?.Invoke(testServices);
            });
        });
}
