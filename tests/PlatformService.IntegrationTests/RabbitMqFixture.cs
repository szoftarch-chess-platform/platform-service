using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using PlatformService.IntegrationTests;
using PlatformService.IntegrationTests.Messaging;
using Testcontainers.RabbitMq;

[assembly: AssemblyFixture(typeof(RabbitMqFixture))]

namespace PlatformService.IntegrationTests;

/// <summary>
/// One RabbitMQ container for the whole test assembly, from the image of the infra compose. Every test that needs the
/// broker gets a virtual host of its own from <see cref="CreateBrokerAsync"/>, so the tests can run in parallel with the
/// same names of the queues.
/// </summary>
public sealed class RabbitMqFixture : IAsyncLifetime
{
    public const string AdminUser = "admin";
    public const string ServiceUser = "platform-service";

    /// <summary>The delivery limit of the policy of the infra repo.</summary>
    public const int DeliveryLimit = 20;

    private const string AdminPassword = "admin-password";
    private const string ServicePassword = "platform-service-password";
    private const ushort AmqpPort = 5672;
    private const ushort ManagementPort = 15672;

    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:4.4-management-alpine")
        .WithUsername(AdminUser)
        .WithPassword(AdminPassword)
        .WithPortBinding(ManagementPort, assignRandomHostPort: true)
        .Build();

    private HttpClient? _management;

    /// <summary>The HTTP API of the management plugin, signed in as the admin user.</summary>
    public HttpClient Management => _management ?? throw new InvalidOperationException("The container has not started.");

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync(TestContext.Current.CancellationToken);

        _management = new HttpClient
        {
            BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(ManagementPort)}/api/"),
        };
        _management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{AdminUser}:{AdminPassword}")));

        await PutAsync($"users/{ServiceUser}", new { password = ServicePassword, tags = string.Empty });
    }

    /// <summary>
    /// A new virtual host with the permissions of ADR-6 for the user of the service and the policy of the infra repo
    /// with shorter delays, so the retries of a test take milliseconds.
    /// </summary>
    internal async Task<TestBroker> CreateBrokerAsync()
    {
        var vhost = $"test-{Guid.NewGuid():N}";
        await PutAsync($"vhosts/{vhost}", new { });
        await PutAsync($"permissions/{vhost}/{AdminUser}", new { configure = ".*", write = ".*", read = ".*" });
        await PutAsync($"permissions/{vhost}/{ServiceUser}", new
        {
            configure = @"^platform\.",
            write = @"^platform\.",
            read = @"^(platform\..*|game\.events)$",
        });

        // The policy of rabbitmq/definitions.json in the infra repo, only the delays are shorter.
        await PutAsync($"policies/{vhost}/platform.game-finished", new Dictionary<string, object>
        {
            ["pattern"] = @"^platform\.game-finished$",
            ["apply-to"] = "quorum_queues",
            ["priority"] = 0,
            ["definition"] = new Dictionary<string, object>
            {
                ["dead-letter-exchange"] = "platform.game-finished.dlx",
                ["dead-letter-strategy"] = "at-least-once",
                ["overflow"] = "reject-publish",
                ["delivery-limit"] = DeliveryLimit,
                ["delayed-retry-type"] = "all",
                ["delayed-retry-min"] = 20,
                ["delayed-retry-max"] = 100,
            },
        });

        return await TestBroker.ConnectAsync(this, vhost);
    }

    /// <summary>The AMQP URI of a virtual host with the user of the service, as compose passes it.</summary>
    public string ServiceUri(string vhost) => AmqpUri(ServiceUser, ServicePassword, vhost);

    /// <summary>The AMQP URI of a virtual host with the admin user, for the part of the game-service in a test.</summary>
    public string AdminUri(string vhost) => AmqpUri(AdminUser, AdminPassword, vhost);

    public async ValueTask DisposeAsync()
    {
        _management?.Dispose();
        await _container.DisposeAsync();
    }

    private string AmqpUri(string user, string password, string vhost) =>
        $"amqp://{user}:{password}@{_container.Hostname}:{_container.GetMappedPublicPort(AmqpPort)}/{Uri.EscapeDataString(vhost)}";

    private async Task PutAsync<T>(string path, T body)
    {
        using var response = await Management.PutAsJsonAsync(path, body, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
