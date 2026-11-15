# PS-ADR-8: Local port, logging and time

**Related:** ADR-3, [PS-ADR-3](request-pipeline.md), [PS-ADR-9](container-image.md)

## Decision

- Locally the service uses http://localhost:5080, which does not collide with the ports that the compose of the infra repo publishes. The gateway is at https://localhost:8443.
- Logging uses the built-in `ILogger`. Outside Development the console logs JSON with scopes and UTC timestamps. Log methods are source generated with `LoggerMessage`.
- Timestamps are `DateTimeOffset` values in UTC. Time-dependent logic takes a `TimeProvider`, registered as `TimeProvider.System`.
