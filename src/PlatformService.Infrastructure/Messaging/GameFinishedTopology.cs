using RabbitMQ.Client;

namespace PlatformService.Infrastructure.Messaging;

/// <summary>
/// The exchanges, queues and bindings of <c>game.finished.v1</c> that the service declares (ADR-6, PS-ADR-14). The
/// policy of the infra repo adds the dead-letter exchange, the delivery limit and the delayed retry to the queue.
/// </summary>
/// <remarks>Every declaration is idempotent, so the consumer repeats them whenever it connects.</remarks>
internal static class GameFinishedTopology
{
    /// <summary>The exchange of the game-service, which the game-service declares.</summary>
    public const string Exchange = "game.events";

    public const string RoutingKey = "game.finished";

    public const string Queue = "platform.game-finished";

    public const string DeadLetterExchange = "platform.game-finished.dlx";

    public const string DeadLetterQueue = "platform.game-finished.dlq";

    /// <summary>Declares the queue and the dead-letter queue with the exchange that leads to it.</summary>
    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            DeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await DeclareQuorumQueueAsync(channel, DeadLetterQueue, cancellationToken);
        await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, string.Empty, cancellationToken: cancellationToken);
        await DeclareQuorumQueueAsync(channel, Queue, cancellationToken);
    }

    /// <summary>
    /// Binds the queue to the exchange of the game-service. While that exchange does not exist, the broker refuses the
    /// binding with 404 and closes the channel.
    /// </summary>
    public static Task BindAsync(IChannel channel, CancellationToken cancellationToken) =>
        channel.QueueBindAsync(Queue, Exchange, RoutingKey, cancellationToken: cancellationToken);

    /// <summary>A durable quorum queue whose only argument is its type, because a policy sets the rest (ADR-6).</summary>
    private static Task DeclareQuorumQueueAsync(IChannel channel, string queue, CancellationToken cancellationToken) =>
        channel.QueueDeclareAsync(
            queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" },
            cancellationToken: cancellationToken);
}
