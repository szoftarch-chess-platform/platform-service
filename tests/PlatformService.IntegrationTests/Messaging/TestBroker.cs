using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PlatformService.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace PlatformService.IntegrationTests.Messaging;

/// <summary>
/// The virtual host of one test, where the test plays the part of the game-service with the admin user of
/// <see cref="RabbitMqFixture"/>.
/// </summary>
internal sealed class TestBroker : IAsyncDisposable
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    private readonly RabbitMqFixture _rabbitMq;
    private readonly IConnection _connection;

    private TestBroker(RabbitMqFixture rabbitMq, string vhost, IConnection connection)
    {
        _rabbitMq = rabbitMq;
        VirtualHost = vhost;
        _connection = connection;
    }

    public string VirtualHost { get; }

    /// <summary>The connection string of the service for this virtual host.</summary>
    public string ServiceUri => _rabbitMq.ServiceUri(VirtualHost);

    public static async Task<TestBroker> ConnectAsync(RabbitMqFixture rabbitMq, string vhost)
    {
        var factory = new ConnectionFactory { Uri = new Uri(rabbitMq.AdminUri(vhost)) };
        return new TestBroker(rabbitMq, vhost, await factory.CreateConnectionAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Declares the exchange of the game-service, as the game-service does when it starts.</summary>
    public async Task DeclareGameEventsAsync()
    {
        await using var channel = await _connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        await channel.ExchangeDeclareAsync(
            GameFinishedTopology.Exchange, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Publishes a body to <c>game.events</c> with the properties of the contract, the mandatory flag and a publisher
    /// confirm, as the game-service does. Until the service has bound its queue, the broker returns the message, and
    /// the test publishes it again.
    /// </summary>
    public async Task PublishAsync(string body, string? messageId = null)
    {
        await using var channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            TestContext.Current.CancellationToken);
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = messageId,
            Type = GameFinishedEvent.TypeName,
        };

        var deadline = DateTimeOffset.UtcNow + _timeout;
        while (true)
        {
            try
            {
                await channel.BasicPublishAsync(
                    GameFinishedTopology.Exchange, GameFinishedTopology.RoutingKey, mandatory: true, properties,
                    Encoding.UTF8.GetBytes(body), TestContext.Current.CancellationToken);
                return;
            }
            catch (PublishReturnException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }
    }

    /// <summary>Publishes an event of <see cref="GameEvents"/>.</summary>
    public Task PublishAsync(GameEvents.Event message) => PublishAsync(message.Json, message.EventId.ToString());

    /// <summary>Waits for the next message of the dead-letter queue and takes it.</summary>
    public async Task<BasicGetResult> NextDeadLetterAsync()
    {
        await using var channel = await _connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        return await Eventually.GetAsync(
            () => channel.BasicGetAsync(GameFinishedTopology.DeadLetterQueue, autoAck: true, TestContext.Current.CancellationToken),
            "a message in the dead-letter queue");
    }

    /// <summary>The number of the messages that are ready in a queue, which leaves out the unacknowledged ones.</summary>
    public async Task<uint> ReadyMessagesAsync(string queue)
    {
        await using var channel = await _connection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        var declared = await channel.QueueDeclarePassiveAsync(queue, TestContext.Current.CancellationToken);
        return declared.MessageCount;
    }

    /// <summary>Waits until the service is connected, and returns the names of its connections.</summary>
    public async Task<IReadOnlyList<string>> ServiceConnectionsAsync()
    {
        return await Eventually.GetAsync<IReadOnlyList<string>>(
            async () =>
            {
                var connections = await _rabbitMq.Management.GetFromJsonAsync<JsonElement>(
                    $"vhosts/{Uri.EscapeDataString(VirtualHost)}/connections", TestContext.Current.CancellationToken);
                var names = connections.EnumerateArray()
                    .Where(connection => connection.GetProperty("user").GetString() == RabbitMqFixture.ServiceUser)
                    .Select(connection => connection.GetProperty("name").GetString()!)
                    .ToList();
                return names.Count > 0 ? names : null;
            },
            "a connection of the service");
    }

    /// <summary>Closes every connection of the service from the side of the broker, as a restart of the broker would.</summary>
    public async Task CloseServiceConnectionsAsync()
    {
        foreach (var name in await ServiceConnectionsAsync())
        {
            using var response = await _rabbitMq.Management.DeleteAsync(
                $"connections/{Uri.EscapeDataString(name)}", TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        using var response = await _rabbitMq.Management.DeleteAsync($"vhosts/{Uri.EscapeDataString(VirtualHost)}");
    }
}
