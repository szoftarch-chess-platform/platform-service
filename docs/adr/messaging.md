# PS-ADR-14: Finished games from RabbitMQ

**Related:** F-6, NF-3, ADR-6, ADR-7, ADR-8, [PS-ADR-1](clean-architecture.md), [PS-ADR-6](validation.md), [PS-ADR-7](persistence.md), [PS-ADR-8](configuration-and-logging.md), [PS-ADR-10](testing.md), [PS-ADR-13](game-history.md)

## Context

The game-service publishes every finished game as the event `game.finished.v1` of `game-events.asyncapi.yaml` to the `game.events` exchange of RabbitMQ (ADR-6). The platform-service consumes it from its own quorum queue `platform.game-finished`, acknowledges it only after the commit, filters out the repeated deliveries and sends what it cannot process to a dead-letter queue. `IFinishedGameService` of PS-ADR-13 records the game. The infra repo runs the broker with the user `platform-service`, its permissions and the policies of the queues (ADR-6). The service has to start and serve the history while the broker is down, and it has to start consuming once the broker is back.

## Decision

- **Client:** `RabbitMQ.Client` in the Infrastructure under `Messaging/`, without a messaging library.
- **Setting:** `ConnectionStrings:RabbitMq` is the AMQP URI of ADR-6 with the user `platform-service`. It is required, and the service checks at startup that it is an `amqp` or `amqps` URI, with the options of PS-ADR-7. The URI is never logged, because it holds the password.
- **Connection:** `GameFinishedConsumer` is a `BackgroundService`. It connects with the name `platform-service`, declares the topology and consumes, until the connection or the channel closes or the broker cancels the consumer. Then it connects again after 5 seconds, which also covers a broker that is down at startup. The automatic recovery of the client is turned off.
- **Topology:** the service declares the quorum queue `platform.game-finished` with a binding to `game.events` with the routing key `game.finished`, the fanout exchange `platform.game-finished.dlx` and the quorum queue `platform.game-finished.dlq` with a binding to it. Everything is durable, and the queues carry only their type as an argument. While `game.events` does not exist, the broker refuses the binding with 404 and closes the channel, so the service tries again every 5 seconds on a new channel.
- **Policy:** the policy `platform.game-finished` of `rabbitmq/definitions.json` in the infra repo, which ADR-6 decides, gives the queue the following:
  - the dead-letter exchange `platform.game-finished.dlx` with at-least-once dead-lettering and the `reject-publish` overflow that it requires
  - the delivery limit 20
  - the delayed retry of the type `all` from 5 to 60 seconds
- **Messages:** the service handles one message at a time with a prefetch of 1. It reads the body as the JSON of the envelope of ADR-6 with the web defaults of System.Text.Json, so it ignores unknown fields and treats `null` like a missing field (ADR-8). The `id` of the envelope is the id of the event, and its `data` becomes `RecordFinishedGameCommand` by a mapping written by hand (PS-ADR-1). The AMQP properties are not read, except `message_id` for the log of a message whose body cannot be read.
- **Outcomes:**
  - A game that is recorded now or was recorded before gets `basic.ack` after the commit of the use case.
  - A body that is not the JSON of the envelope, a `type` other than `game.finished.v1` and a game that the validator of PS-ADR-13 refuses get `basic.reject` without requeue, so the message goes to the dead-letter queue at once. The service logs this as a warning.
  - A transient error of the database gets `basic.nack` with requeue. The error is transient when a `DbException` with `IsTransient` or a `TimeoutException` is in the chain of its inner exceptions. The service logs this as a warning.
  - Any other exception gets `basic.reject` with requeue, which counts toward the delivery limit. The service logs this as an error.
  - A message that arrives while the service stops, or whose handling the stop cancels, gets `basic.nack` with requeue.
- **Stopping:** the service cancels its consumer and the handling of the message in progress, waits until that message has its answer, and then closes the channel and the connection.
- **Readiness:** the readiness check of PS-ADR-7 does not include the broker.
- **Logs:** the log methods use `LoggerMessage` as PS-ADR-8 describes, with the ids of the event and of the game.

## Rationale

- The contract defines the envelope of the event and the topology of ADR-6 names every exchange and queue, so a library that brings its own envelope and naming would have to be configured around both. MassTransit wraps every message in its own envelope, and it has a commercial license from version 9.
- Version 7.3.0 is the latest stable version, and with its release 7.2.x and 6.x get no more releases, not even security fixes.
- The automatic recovery of the client does not cover a failed first connection, a channel that an error closed or a consumer that the broker cancelled, so the service needs a loop of its own anyway. A consumer that both the service and the topology recovery register is a protocol error of the connection. With one loop every failure takes the same path, and the declarations are idempotent. The receiving side of the NServiceBus transport for RabbitMQ also connects again on its own.
- Since RabbitMQ 4.3 a `basic.nack` does not count toward the delivery limit, while a `basic.reject` does. So a database outage of any length sends no game to the dead-letter queue, while a message that keeps failing for another reason gets there after the limit. RabbitMQ advises `basic.reject` for such messages, because with `basic.nack` they would come back forever.
- The delayed retry of quorum queues exists since RabbitMQ 4.3. The broker holds the message back, so the consumer goes on with the other messages, and the message is not rewritten. The documentation of dead-lettering calls it the recommended approach. Its delay grows with the failed deliveries, so a message that keeps failing gets to the dead-letter queue after about a quarter of an hour, while a transient error waits 5 seconds each time.
- Without at-least-once dead-lettering, RabbitMQ drops a dead-lettered message that the dead-letter queue does not accept, and a game must not be lost. The queue has no length limit, so the `reject-publish` overflow never refuses a message.
- RabbitMQ strongly recommends policies over the arguments of a queue, so the delivery limit and the delays can change without a new version of the service (ADR-6).
- A message that the validator refuses or that cannot be read fails again on every delivery, so it goes to the dead-letter queue at once. A new value of an enum passes (PS-ADR-13), while an unknown `type` is a version of the event that this version of the service cannot read, so it waits in the dead-letter queue (ADR-7).
- The rules of the command are in the Application (PS-ADR-6), so the consumer only reads and maps the envelope.
- The games come at the pace of the players, so throughput does not matter. A connection that closes counts a failed delivery for every message that is not acknowledged yet, so with one message at a time at most one message gets that count.
- Without the broker only the new games wait in the queue, while the accounts and the history still work, so taking the service out of service would hide more than it saves.

## Rejected alternatives

- **MassTransit, or another messaging library such as Wolverine or Rebus:** retries and dead-lettering come with it, but its envelope and topology would have to be configured around the contract, and MassTransit has a commercial license from version 9.
- **The automatic recovery of the client:** the service would still need code for the first connection, the closed channels and the cancelled consumers, and that code would compete with the recovery.
- **A delay in the consumer before the requeue:** it holds back the other messages for the time of the delay.
- **A retry queue with a TTL that dead-letters back into the queue:** the usual pattern before RabbitMQ 4.3, but it needs two more resources, and RabbitMQ rewrites the message on the way.
- **The same outcome for every exception:** with `basic.nack` a message that keeps failing would never reach the dead-letter queue, and with `basic.reject` a database outage of a quarter of an hour would send the games there.
- **The type and the id from the AMQP properties:** the contract repeats them there, but the envelope is the payload that the schema of the contract describes.
- **The broker in the readiness check:** see the rationale above.

## Consequences

- On a broker without the policy of the infra repo, a refused message is lost (ADR-6). The tests set the same policy with shorter delays.
- The names in the policy have to follow the names of the queue and the dead-letter exchange of the service.
- A message in the dead-letter queue waits there until someone handles it in the web management interface of RabbitMQ.
- A repeated delivery is acknowledged without a change (PS-ADR-13).
- The integration tests also start RabbitMQ in a container (PS-ADR-10).

## Sources

- RabbitMQ .NET client API guide: https://www.rabbitmq.com/client-libraries/dotnet-api-guide
- RabbitMQ .NET client 7.3.0: https://github.com/rabbitmq/rabbitmq-dotnet-client/releases/tag/v7.3.0
- RabbitMQ quorum queues, delivery limit and delayed retry: https://www.rabbitmq.com/docs/quorum-queues
- RabbitMQ dead letter exchanges: https://www.rabbitmq.com/docs/dlx
- RabbitMQ consumer acknowledgements and publisher confirms: https://www.rabbitmq.com/docs/confirms
- RabbitMQ consumer prefetch: https://www.rabbitmq.com/docs/consumer-prefetch
- RabbitMQ access control: https://www.rabbitmq.com/docs/access-control
- NServiceBus RabbitMQ transport, message pump: https://github.com/Particular/NServiceBus.RabbitMQ/blob/master/src/NServiceBus.Transport.RabbitMQ/Receiving/MessagePump.cs
- MassTransit v9 announcement: https://masstransit.io/introduction/v9-announcement
- DbException.IsTransient: https://learn.microsoft.com/dotnet/api/system.data.common.dbexception.istransient
- Idempotent Consumer pattern: https://microservices.io/patterns/communication-style/idempotent-consumer.html
