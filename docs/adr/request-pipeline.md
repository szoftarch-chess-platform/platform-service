# PS-ADR-3: Request pipeline

**Related:** ADR-3, ADR-4, [PS-ADR-4](gateway-authentication.md), [PS-ADR-5](error-handling.md), [PS-ADR-8](configuration-and-logging.md)

## Context

The middleware order of `Program.cs` is the path of a request through the service.

## Decision

The middleware runs in this order.

- **ForwardedHeaders** reads `X-Forwarded-For` and `X-Forwarded-Proto` only from the networks in `ForwardedHeaders:KnownNetworks`. This CIDR list is in `appsettings.json` with the private ranges and must not be empty. There is no `UseHttpsRedirection`, HSTS, CORS or rate limiting, because the gateway is in front of the service.
- **The exception handler and the status code pages** come next.
- **Correlation** puts the `x-request-id` of the gateway into the logging scope as `CorrelationId`. If the header is missing, `TraceIdentifier` is used. The name is not `RequestId`, because the own logging scope of ASP.NET Core already uses that for the `TraceIdentifier`.
- **Authentication and authorization** come from the identity headers.
- **The controller** comes last.
