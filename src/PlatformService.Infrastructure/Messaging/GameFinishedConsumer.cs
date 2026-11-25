using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace PlatformService.Infrastructure.Messaging;

/// <summary>
/// Consumes <c>game.finished.v1</c> from <c>platform.game-finished</c> one message at a time (ADR-6, PS-ADR-14). It
/// connects, declares the topology and consumes until the connection or the channel closes or the broker cancels the
/// consumer. Then it connects again after a delay, which also covers a broker that is down at startup.
/// </summary>
/// <remarks>
/// The automatic recovery of the client is turned off, because this loop covers every failure, and a consumer that both
/// this loop and the topology recovery register is a protocol error.
/// </remarks>
internal sealed partial class GameFinishedConsumer(
    IOptions<RabbitMqOptions> options,
    GameFinishedHandler handler,
    TimeProvider timeProvider,
    ILogger<GameFinishedConsumer> logger) : BackgroundService
{
    /// <summary>The name of the connection in the web management interface of RabbitMQ.</summary>
    public const string ConnectionName = "platform-service";

    private readonly RabbitMqOptions _options = options.Value;

    // The handling of the last delivery, which the stop waits for before it closes the channel.
    private Task _handling = Task.CompletedTask;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        do
        {
            try
            {
                var reason = await ConsumeAsync(stoppingToken);
                if (!stoppingToken.IsCancellationRequested)
                {
                    LogStopped(logger, reason, _options.RetryDelay);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // For example the broker is down or refuses a declaration. An unhandled exception would stop the
                // whole service.
                LogConsumingFailed(logger, exception, _options.RetryDelay);
            }
        }
        while (await WaitAsync(_options.RetryDelay, stoppingToken));
    }

    /// <summary>Consumes on one connection, and returns why consuming stopped.</summary>
    private async Task<string> ConsumeAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_options.ConnectionString),
            ClientProvidedName = ConnectionName,
            AutomaticRecoveryEnabled = false,
            TopologyRecoveryEnabled = false,
        };
        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await DeclareAsync(connection, stoppingToken);

        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, stoppingToken);

        var stopped = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) =>
        {
            _handling = HandleDeliveryAsync(channel, delivery, stoppingToken);
            return _handling;
        };
        consumer.ShutdownAsync += (_, shutdown) =>
        {
            stopped.TrySetResult($"the channel closed with {shutdown.ReplyCode} {shutdown.ReplyText}");
            return Task.CompletedTask;
        };
        consumer.UnregisteredAsync += (_, _) =>
        {
            stopped.TrySetResult("the broker cancelled the consumer");
            return Task.CompletedTask;
        };

        var consumerTag = await channel.BasicConsumeAsync(GameFinishedTopology.Queue, autoAck: false, consumer, stoppingToken);
        LogConsuming(logger, GameFinishedTopology.Queue);

        string reason;
        await using (stoppingToken.Register(() => stopped.TrySetResult("the service stops")))
        {
            reason = await stopped.Task;
        }

        if (stoppingToken.IsCancellationRequested && channel.IsOpen)
        {
            await StopConsumingAsync(channel, consumerTag);
        }

        return reason;
    }

    /// <summary>Declares the topology, and repeats the binding until the exchange of the game-service exists.</summary>
    private async Task DeclareAsync(IConnection connection, CancellationToken stoppingToken)
    {
        await using (var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken))
        {
            await GameFinishedTopology.DeclareAsync(channel, stoppingToken);
        }

        while (true)
        {
            try
            {
                // A refused binding closes its channel, so every try gets a new one.
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await GameFinishedTopology.BindAsync(channel, stoppingToken);
                return;
            }
            catch (OperationInterruptedException exception) when (exception.ShutdownReason?.ReplyCode == Constants.NotFound)
            {
                LogWaitingForExchange(logger, GameFinishedTopology.Exchange, _options.RetryDelay);
                await Task.Delay(_options.RetryDelay, timeProvider, stoppingToken);
            }
        }
    }

    private async Task HandleDeliveryAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        try
        {
            // A message that arrives while the service stops goes back without counting toward the delivery limit.
            var outcome = stoppingToken.IsCancellationRequested
                ? DeliveryOutcome.Retry
                : await handler.HandleAsync(delivery.Body, delivery.BasicProperties.MessageId, stoppingToken);
            await AnswerAsync(channel, delivery.DeliveryTag, outcome);
        }
        catch (Exception exception)
        {
            // For example the channel closed before the answer, so the broker delivers the message again.
            LogAnswerFailed(logger, exception, delivery.DeliveryTag);
        }
    }

    /// <summary>The answer goes out without the token of the stop, so a message is not left without one.</summary>
    private static async Task AnswerAsync(IChannel channel, ulong deliveryTag, DeliveryOutcome outcome)
    {
        switch (outcome)
        {
            case DeliveryOutcome.Acknowledge:
                await channel.BasicAckAsync(deliveryTag, multiple: false);
                break;
            case DeliveryOutcome.Retry:
                await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: true);
                break;
            case DeliveryOutcome.Fail:
                await channel.BasicRejectAsync(deliveryTag, requeue: true);
                break;
            case DeliveryOutcome.DeadLetter:
                await channel.BasicRejectAsync(deliveryTag, requeue: false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "The outcome has no answer to the broker.");
        }
    }

    /// <summary>Cancels the consumer and waits for the message in progress, whose handling the stop has cancelled.</summary>
    private async Task StopConsumingAsync(IChannel channel, string consumerTag)
    {
        try
        {
            await channel.BasicCancelAsync(consumerTag);
            await _handling;
        }
        catch (Exception exception)
        {
            // The connection closes anyway, and the broker delivers an unanswered message again.
            LogStopFailed(logger, exception);
        }
    }

    /// <summary>Waits before the next connection, and returns false when the service stops meanwhile.</summary>
    private async Task<bool> WaitAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, timeProvider, stoppingToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Consuming the queue {Queue}.")]
    private static partial void LogConsuming(ILogger logger, string queue);

    [LoggerMessage(Level = LogLevel.Information, Message = "The exchange {Exchange} does not exist yet, so the binding is tried again in {RetryDelay}.")]
    private static partial void LogWaitingForExchange(ILogger logger, string exchange, TimeSpan retryDelay);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consuming stopped because {Reason}, and the consumer connects again in {RetryDelay}.")]
    private static partial void LogStopped(ILogger logger, string reason, TimeSpan retryDelay);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Consuming from RabbitMQ failed, and the consumer connects again in {RetryDelay}.")]
    private static partial void LogConsumingFailed(ILogger logger, Exception exception, TimeSpan retryDelay);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The answer for the delivery {DeliveryTag} did not reach the broker, so the broker delivers the message again.")]
    private static partial void LogAnswerFailed(ILogger logger, Exception exception, ulong deliveryTag);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The consumer did not stop cleanly, so the broker delivers an unanswered message again.")]
    private static partial void LogStopFailed(ILogger logger, Exception exception);
}
