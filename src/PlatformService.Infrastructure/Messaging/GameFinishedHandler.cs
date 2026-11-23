using System.Data.Common;
using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlatformService.Application.Games;

namespace PlatformService.Infrastructure.Messaging;

/// <summary>
/// Records the game of one <c>game.finished.v1</c> message with <see cref="IFinishedGameService"/>, and decides how the
/// consumer answers the broker (PS-ADR-14).
/// </summary>
internal sealed partial class GameFinishedHandler(IServiceScopeFactory scopeFactory, ILogger<GameFinishedHandler> logger)
{
    // The web defaults read camelCase names, ignore unknown fields and accept null for a missing value (ADR-8).
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <param name="body">The body of the message.</param>
    /// <param name="messageId">The <c>message_id</c> property, only for the log of a body that cannot be read.</param>
    /// <param name="cancellationToken">Cancelled when the service stops.</param>
    public async Task<DeliveryOutcome> HandleAsync(
        ReadOnlyMemory<byte> body, string? messageId, CancellationToken cancellationToken)
    {
        GameFinishedEvent? message;
        try
        {
            message = JsonSerializer.Deserialize<GameFinishedEvent>(body.Span, _jsonOptions);
        }
        catch (JsonException exception)
        {
            LogUnreadable(logger, exception, messageId);
            return DeliveryOutcome.DeadLetter;
        }

        if (message is null)
        {
            LogUnreadable(logger, null, messageId);
            return DeliveryOutcome.DeadLetter;
        }

        if (message.Type != GameFinishedEvent.TypeName)
        {
            LogUnknownType(logger, message.Id, message.Type);
            return DeliveryOutcome.DeadLetter;
        }

        return await RecordAsync(message, cancellationToken);
    }

    /// <summary>
    /// Whether a <see cref="DbException"/> that reports itself as transient or a <see cref="TimeoutException"/> is in
    /// the chain of the inner exceptions, so the same message may succeed later.
    /// </summary>
    public static bool IsTransient(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException { IsTransient: true } or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<DeliveryOutcome> RecordAsync(GameFinishedEvent message, CancellationToken cancellationToken)
    {
        var command = message.ToCommand();
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var outcome = await scope.ServiceProvider.GetRequiredService<IFinishedGameService>()
                .RecordAsync(command, cancellationToken);
            if (outcome == RecordOutcome.Recorded)
            {
                LogRecorded(logger, command.GameId, command.EventId);
            }
            else
            {
                LogRecordedBefore(logger, command.GameId, command.EventId);
            }

            return DeliveryOutcome.Acknowledge;
        }
        catch (ValidationException exception)
        {
            LogInvalid(logger, command.EventId, exception.Message);
            return DeliveryOutcome.DeadLetter;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The service stops, and the game may be stored already, which the next delivery finds.
            return DeliveryOutcome.Retry;
        }
        catch (Exception exception) when (IsTransient(exception))
        {
            LogTransientError(logger, exception, command.EventId);
            return DeliveryOutcome.Retry;
        }
        catch (Exception exception)
        {
            LogError(logger, exception, command.EventId);
            return DeliveryOutcome.Fail;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Recorded the game {GameId} of the event {EventId}.")]
    private static partial void LogRecorded(ILogger logger, Guid gameId, Guid eventId);

    [LoggerMessage(Level = LogLevel.Information, Message = "The game {GameId} of the event {EventId} was recorded before.")]
    private static partial void LogRecordedBefore(ILogger logger, Guid gameId, Guid eventId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The message {MessageId} is not the JSON of an event, so it goes to the dead-letter queue.")]
    private static partial void LogUnreadable(ILogger logger, Exception? exception, string? messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The event {EventId} has the unknown type {Type}, so it goes to the dead-letter queue.")]
    private static partial void LogUnknownType(ILogger logger, Guid eventId, string? type);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The event {EventId} breaks the contract, so it goes to the dead-letter queue. {Errors}")]
    private static partial void LogInvalid(ILogger logger, Guid eventId, string errors);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The event {EventId} failed with a transient error, and it comes back after the delayed retry.")]
    private static partial void LogTransientError(ILogger logger, Exception exception, Guid eventId);

    [LoggerMessage(Level = LogLevel.Error, Message = "The event {EventId} failed, and it comes back after the delayed retry until the delivery limit sends it to the dead-letter queue.")]
    private static partial void LogError(ILogger logger, Exception exception, Guid eventId);
}
