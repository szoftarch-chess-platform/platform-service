# platform-service documentation

- [Architecture](architecture.md): diagrams of the layers and of the request path
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
| [PS-ADR-10](adr/testing.md) | Tests with xUnit v3 and hand-written fakes |

## Open questions

- Should registration, login, refresh, logout and password hashing use ASP.NET Core Identity or a custom users table?
- The open questions of [PS-ADR-7](adr/persistence.md) on the migrations and the UUID v7 values.

## Next steps

- Answer the authentication question, then build the part of ADR-4 that belongs to this service.
- Answer the open questions of PS-ADR-7 together with the first entity.
- Build the part of ADR-6 that belongs to this service, then the game history of F-6.
- Publish the OpenAPI document of the service among the contracts of ADR-2.
