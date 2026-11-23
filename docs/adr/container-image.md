# PS-ADR-9: Chiseled container image

**Related:** NF-9, ADR-2, [PS-ADR-1](clean-architecture.md), [PS-ADR-2](http-api.md), [PS-ADR-8](configuration-and-logging.md), [PS-ADR-10](testing.md), [PS-ADR-11](accounts.md), [PS-ADR-14](messaging.md)

## Context

The service runs as a container image, which has to work on amd64 and arm64 machines. The compose of the infra repo (ADR-2) starts the gateway only once the services behind it are ready, and it learns this from the health check of each container.

## Decision

- The multi-stage `Dockerfile` builds with `sdk:10.0` using `--platform=$BUILDPLATFORM` and `-a $TARGETARCH`, so the build needs no emulation. It restores in a separate layer from the csproj and `Directory.*.props` files.
- The runtime image is `aspnet:10.0-noble-chiseled`. It runs as a non-root user through `$APP_UID` and listens on port 8080 through `ASPNETCORE_HTTP_PORTS`.
- The image contains the health probe `PlatformService.HealthProbe` in the folder `health-probe`. It is a console app that asks the URL of its only argument with a timeout of 4 seconds. It exits with 0 for a success status code, and with 1 for any other answer, for no answer and for an argument that is not an http URL, as Docker expects. It writes the reason of a failure to stderr. It references no project and no package.
- The compose of the infra repo runs the probe with `http://127.0.0.1:8080/health/ready` as the health check of the container, so the gateway starts once the service reaches its database (PS-ADR-2). The `Dockerfile` has no `HEALTHCHECK`, like the images of the other services.
- In the image, add `GSS Encryption Mode=Disable` to the connection string. The image has no Kerberos library. Npgsql tries GSS encryption by default and then writes the harmless error `Cannot load library libgssapi_krb5.so.2` to stderr, which this setting removes.

## Rationale

- The chiseled image has no shell, curl or wget, so a health check can only start a program of the image directly. The .NET team discusses this gap in issue 4300 of dotnet/dotnet-docker. The images ship no tool for it, and the issue describes the pattern of a small .NET program that sends the request, which a team builds itself. A maintainer calls it a need of Docker Compose, because Kubernetes sends its probes from outside the container.
- The SDK that builds the service also builds the probe for the same architecture, and the probe runs on the .NET runtime of the image. So the image gets no other toolchain and no downloaded binary, and the probe adds about 100 kilobytes, most of it the executable host of .NET.
- A check starts a .NET process that ends after one request, which takes about a quarter of a second in compose.
- The readiness endpoint checks the database and not the broker (PS-ADR-14), so the gateway waits for what the API needs.
- Docker keeps the output of the last checks, which `docker inspect` shows, so the reason of a failure is visible without the logs of the service.

## Rejected alternatives

- **`HEALTHCHECK` in the `Dockerfile`:** the compose of the infra repo holds the health checks of every service with the same timing, and Kubernetes ignores `HEALTHCHECK`.
- **A flag of the service itself, such as `--health-check`:** every check would load the assemblies of the whole service, and `Program.cs` would mix the health check with the composition root (PS-ADR-1).
- **A static binary of a third party, such as microcheck:** a smaller process, but a downloaded binary in the image of the service, whose releases the team would have to follow.
- **An image with a shell and curl:** a health check with curl, but a larger image with a shell and a package manager, which the chiseled image leaves out on purpose.
- **The active health checking of Envoy:** the gateway would send requests only to a ready instance, but compose would still start it before the service is ready, and `docker compose up --wait` would not wait for the service.

## Consequences

- The image has no ICU or time zone data, so .NET runs in invariant globalization mode. Timestamps are in UTC anyway. Unicode normalization does nothing in this mode, which the open question of PS-ADR-11 concerns.
- The `Dockerfile` restores and publishes the service and the probe separately.
- The unit tests cover the probe (PS-ADR-10).

## Sources

- dotnet/dotnet-docker issue 4300, a health check utility for distroless images: https://github.com/dotnet/dotnet-docker/issues/4300
- Dockerfile reference, HEALTHCHECK: https://docs.docker.com/reference/dockerfile/#healthcheck
- Compose file reference, healthcheck: https://docs.docker.com/reference/compose-file/services/#healthcheck
