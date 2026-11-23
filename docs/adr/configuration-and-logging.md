# PS-ADR-8: Local port, logging and time

**Related:** ADR-3, ADR-6, [PS-ADR-3](request-pipeline.md), [PS-ADR-4](gateway-authentication.md), [PS-ADR-9](container-image.md), [PS-ADR-12](tokens-and-sessions.md), [PS-ADR-14](messaging.md)

## Decision

- Locally the service uses http://localhost:8080, the same port as in its container. With the override file of the infra repo for a platform-service on the host, the gateway at https://localhost:8443 reaches it there (ADR-6).
- Logging uses the built-in `ILogger`. Outside Development the console logs JSON with scopes and UTC timestamps. Log methods are source generated with `LoggerMessage`.
- `appsettings.json` logs `Information` and above, except for the categories `Microsoft.AspNetCore` and `Microsoft.EntityFrameworkCore.Database.Command`, which log `Warning` and above. So EF Core logs a SQL command only when it fails, as an error. In Development `appsettings.Development.json` logs every command at `Information` with its text and duration, without the values of its parameters.
- Timestamps are `DateTimeOffset` values in UTC. Time-dependent logic takes a `TimeProvider`, registered as `TimeProvider.System`.

## Rationale

- With the same port on the host and in the container the Envoy configuration of the infra repo needs no port for each setup, as ADR-6 decides. The compose of the infra repo publishes no port 8080.
- The logging guidance of ASP.NET Core says that the levels Trace, Debug and Information produce a high volume of messages in production, and it advises to limit them to specific categories. EF Core logs every command at `Information`, which is one or more lines for each request, while the own messages of the service, such as the security events of PS-ADR-12 and the finished games of PS-ADR-14, are few.
- In Development the text of the commands helps to see what EF Core sends to the database.
- The base class of `GatewayAuthenticationHandler` of PS-ADR-4 logs every challenge at `Information`, because its category is outside `Microsoft.AspNetCore`. This stays, because the gateway lets a request without a token through only under `/api/auth`, where every endpoint allows anonymous access, so only a request that no endpoint serves, such as one with a wrong path or a wrong method, gets a challenge.

## Sources

- Logging in ASP.NET Core, log level: https://learn.microsoft.com/aspnet/core/fundamentals/logging/#log-level
