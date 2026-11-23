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
| [PS-ADR-14](adr/messaging.md) | Finished games from RabbitMQ |

## Open questions

- The open question of [PS-ADR-8](adr/configuration-and-logging.md) on the log levels outside Development.
- The open question of [PS-ADR-11](adr/accounts.md) on the NFC normalization of the passwords.
