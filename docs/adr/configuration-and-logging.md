# PS-ADR-8: Local port, logging and time

**Related:** [PS-ADR-3](request-pipeline.md), [PS-ADR-9](container-image.md)

## Decision

- Locally the service uses http://localhost:5080, because port 8080 belongs to the gateway according to the `.env.example` of the frontend.
- Logging uses the built-in `ILogger`. Outside Development the console logs JSON with scopes and UTC timestamps. Log methods are source generated with `LoggerMessage`.
- Timestamps are `DateTimeOffset` values in UTC. Time-dependent logic takes a `TimeProvider`, registered as `TimeProvider.System`.
