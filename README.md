# platform-service

ASP.NET Core service of the online chess platform. Its responsibilities are decided in ADR-1.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Docker for building the image
- The infra repo in the sibling folder, set up as its README describes

## Configuration

Settings come from `appsettings.json` and environment variables, where every `:` of a key becomes `__`. Locally the secrets go into `dotnet user-secrets`.

| Setting | Environment variable | Required | Description |
|---|---|---|---|
| `ConnectionStrings:Platform` | `ConnectionStrings__Platform` | yes | Npgsql connection string of the database of the service |
| `ForwardedHeaders:KnownNetworks` | `ForwardedHeaders__KnownNetworks__0`, `__1` and so on | yes, set in `appsettings.json` | CIDR ranges that may send `X-Forwarded-For` and `X-Forwarded-Proto`. Environment variables override the entries of `appsettings.json` by index, and entries with a higher index stay. |
| `ASPNETCORE_ENVIRONMENT` | | no | `Development` turns on the development features that PS-ADR-2, PS-ADR-5 and PS-ADR-8 describe |

## Running locally

The service uses the PostgreSQL of the infra compose. Start it in the infra folder.

```bash
docker compose up -d --wait postgres
```

Set the connection string once with the password `PLATFORM_DB_PASSWORD` of the `.env` of the infra repo, then run the service on http://localhost:8080.

```bash
dotnet user-secrets set "ConnectionStrings:Platform" "Host=localhost;Port=5432;Database=platform;Username=platform;Password=<PLATFORM_DB_PASSWORD>" --project src/PlatformService.Api
dotnet run --project src/PlatformService.Api
```

On Linux add `--urls "http://localhost:8080;http://172.17.0.1:8080"` to `dotnet run` when the gateway has to reach the service, with the address of the Docker bridge that the README of the infra repo describes.

To reach the service through the gateway at https://localhost:8443, add `compose.platform-on-host.yaml` to the compose files, as the README of the infra repo describes for a service on the host.

Without the gateway send the identity headers yourself. In Windows PowerShell use `curl.exe`.

```bash
curl -i http://localhost:8080/api/users/me -H "x-user-id: 0193c2b4-7d3e-7c2a-9f1e-3b5a6c7d8e9f" -H "x-user-name: alice"
# 200 {"id":"0193c2b4-7d3e-7c2a-9f1e-3b5a6c7d8e9f","userName":"alice"}

curl -i http://localhost:8080/api/users/me
# 401 application/problem+json
```

In Development the API reference is at http://localhost:8080/scalar. Its authentication panel has a field for both headers. The endpoints are decided in [PS-ADR-2](docs/adr/http-api.md), and the error responses in [PS-ADR-5](docs/adr/error-handling.md).

## Tests

```bash
dotnet test
```

The tests need no database, and the Test Explorer of Visual Studio finds them as well.

## Docker

```bash
# Image for the local platform
docker build -t platform-service .

# Image for both amd64 and arm64
docker buildx build --platform linux/amd64,linux/arm64 -t platform-service .

# Run it on the database network of the infra compose, next to its PostgreSQL
docker run --rm --network chess-platform_postgres -p 127.0.0.1:8081:8080 -e "ConnectionStrings__Platform=Host=postgres;Database=platform;Username=platform;Password=<PLATFORM_DB_PASSWORD>;GSS Encryption Mode=Disable" platform-service
curl -i http://localhost:8081/health/live
```

[PS-ADR-9](docs/adr/container-image.md) describes the image and the `GSS Encryption Mode=Disable` part of the connection string.

## File structure

```
platform-service/
├── PlatformService.slnx
├── global.json                  # SDK version and test runner
├── Directory.Build.props        # build settings of every project
├── Directory.Packages.props     # versions of every package
├── .editorconfig, .gitignore, .dockerignore
├── Dockerfile                   # container image
├── docs/                        # decisions and diagrams of the service
├── src/
│   ├── PlatformService.Api/                  # presentation and composition root
│   │   ├── Program.cs                        # DI calls and the middleware order
│   │   ├── DependencyInjection.cs            # AddPresentation
│   │   ├── Authentication/                   # Gateway scheme and the ICurrentUser implementation
│   │   ├── Controllers/                      # API controllers
│   │   ├── Correlation/                      # correlation id in the logging scope
│   │   ├── ErrorHandling/                    # exceptions to ProblemDetails
│   │   ├── OpenApi/                          # transformers of the OpenAPI document
│   │   ├── appsettings.json
│   │   └── Properties/launchSettings.json    # local launch profile
│   ├── PlatformService.Application/          # use cases, DTOs, ports and validators
│   │   ├── DependencyInjection.cs            # AddApplication
│   │   ├── Abstractions/                     # ports
│   │   ├── Common/Exceptions/                # exceptions that become error responses
│   │   └── Users/                            # use cases of the users
│   ├── PlatformService.Domain/               # entities and domain rules
│   └── PlatformService.Infrastructure/       # EF Core and the port implementations
│       ├── DependencyInjection.cs            # AddInfrastructure
│       ├── HealthCheckTags.cs                # tags of the health checks
│       └── Persistence/                      # DbContext and its options
└── tests/
    ├── PlatformService.UnitTests/            # tests of the Application logic
    └── PlatformService.IntegrationTests/     # tests of the whole app in memory
```

## Documentation

The decisions and the diagrams of the service are in [docs/](docs/README.md).
