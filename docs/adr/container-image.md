# PS-ADR-9: Chiseled container image

**Related:** NF-9, ADR-2, [PS-ADR-2](http-api.md), [PS-ADR-8](configuration-and-logging.md), [PS-ADR-11](accounts.md)

## Context

The service runs as a container image, which has to work on amd64 and arm64 machines.

## Decision

- The multi-stage `Dockerfile` builds with `sdk:10.0` using `--platform=$BUILDPLATFORM` and `-a $TARGETARCH`, so the build needs no emulation. It restores in a separate layer from the csproj and `Directory.*.props` files.
- The runtime image is `aspnet:10.0-noble-chiseled`. It runs as a non-root user through `$APP_UID` and listens on port 8080 through `ASPNETCORE_HTTP_PORTS`.
- The chiseled image has no shell or curl, so there is no `HEALTHCHECK`. The infra side checks the health endpoints from outside, for example with the active health checking of Envoy.
- In the image, add `GSS Encryption Mode=Disable` to the connection string. The image has no Kerberos library. Npgsql tries GSS encryption by default and then writes the harmless error `Cannot load library libgssapi_krb5.so.2` to stderr, which this setting removes.

## Consequences

- The image has no ICU or time zone data, so .NET runs in invariant globalization mode. Timestamps are in UTC anyway. Unicode normalization does nothing in this mode, which the open question of PS-ADR-11 concerns.
