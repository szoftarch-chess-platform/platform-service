# platform-service documentation

- [Architecture](architecture.md): diagrams of the layers, of the request path, of registration and of the refresh
- System documentation: the `docs/` folder of the infra repo

## ADRs

| ID | Title |
|---|---|
| [PS-ADR-1](adr/clean-architecture.md) | Clean Architecture in four projects |
| [PS-ADR-2](adr/http-api.md) | HTTP API and its documentation |
| [PS-ADR-3](adr/request-pipeline.md) | Request pipeline |
| [PS-ADR-4](adr/gateway-authentication.md) | Users from the gateway headers |
| [PS-ADR-5](adr/error-handling.md) | Errors as ProblemDetails |
| [PS-ADR-6](adr/validation.md) | Validation with FluentValidation |
| [PS-ADR-7](adr/persistence.md) | Persistence with EF Core |
| [PS-ADR-8](adr/configuration-and-logging.md) | Local port, logging and time |
| [PS-ADR-9](adr/container-image.md) | Chiseled container image |
| [PS-ADR-10](adr/testing.md) | Tests with xUnit v3, hand-written fakes and Testcontainers |
| [PS-ADR-11](adr/accounts.md) | User accounts with ASP.NET Core Identity |
| [PS-ADR-12](adr/tokens-and-sessions.md) | Access tokens and sessions |
| [PS-ADR-13](adr/game-history.md) | Game history |

## Open questions

- The open question of [PS-ADR-8](adr/configuration-and-logging.md) on the log levels outside Development.
- The open question of [PS-ADR-11](adr/accounts.md) on the NFC normalization of the passwords.
- The RabbitMQ client. It has to read the envelope of ADR-6 as plain JSON and acknowledge only after the commit.
- The name of the dead-letter queue and the delivery limit (ADR-6), within the `platform.` prefix.

## Next steps

- Consume `game.finished.v1` of the contract `game-events.asyncapi.yaml` from the `platform.game-finished` quorum queue with the binding and the dead-letter queue of ADR-6, and record every game with `IFinishedGameService` of [PS-ADR-13](adr/game-history.md), which filters out the repeated deliveries. Test it with the examples of the contract sent through `rabbitmqadmin` (ADR-7). Describe in the README how the service connects locally to the RabbitMQ of the infra compose (ADR-6).
