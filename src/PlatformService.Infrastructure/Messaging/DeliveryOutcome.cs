namespace PlatformService.Infrastructure.Messaging;

/// <summary>The answer of the consumer to the broker for one message (PS-ADR-14).</summary>
internal enum DeliveryOutcome
{
    /// <summary><c>basic.ack</c>, because the game is stored.</summary>
    Acknowledge,

    /// <summary>
    /// <c>basic.nack</c> with requeue for a transient error. The message comes back after the delayed retry, and
    /// RabbitMQ does not count it toward the delivery limit.
    /// </summary>
    Retry,

    /// <summary>
    /// <c>basic.reject</c> with requeue for any other error. The message comes back after the delayed retry, and after
    /// the delivery limit it goes to the dead-letter queue.
    /// </summary>
    Fail,

    /// <summary><c>basic.reject</c> without requeue, so the message goes to the dead-letter queue at once.</summary>
    DeadLetter,
}
