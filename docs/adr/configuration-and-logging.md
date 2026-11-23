# PS-ADR-8: Local port, logging and time

**Related:** ADR-3, ADR-6, [PS-ADR-3](request-pipeline.md), [PS-ADR-9](container-image.md)

## Decision

- Locally the service uses http://localhost:8080, the same port as in its container. With the override file of the infra repo for a platform-service on the host, the gateway at https://localhost:8443 reaches it there (ADR-6).
- Logging uses the built-in `ILogger`. Outside Development the console logs JSON with scopes and UTC timestamps. Log methods are source generated with `LoggerMessage`.
- Timestamps are `DateTimeOffset` values in UTC. Time-dependent logic takes a `TimeProvider`, registered as `TimeProvider.System`.

## Rationale

- With the same port on the host and in the container the Envoy configuration of the infra repo needs no port for each setup, as ADR-6 decides. The compose of the infra repo publishes no port 8080.

## Open questions

- Which log levels the service writes outside Development. `appsettings.json` sets `Information` for every category except `Microsoft.AspNetCore`, so EF Core logs every SQL command with its text and duration but without the values of its parameters. The base class of `GatewayAuthenticationHandler` logs every challenge the same way, because the category of the handler is outside `Microsoft.AspNetCore`. With `Warning` for `Microsoft.EntityFrameworkCore.Database.Command` only the failed commands would be logged, as errors.
