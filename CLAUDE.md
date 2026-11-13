# Platform Service

ASP.NET Core service of the online chess platform. It owns the users and every feature that is not real-time, which means game history now and friends and Élő ratings later. It is one repo of a microservice system. The sibling repos `frontend`, `game-service`, `engine-service` and `infra` each have their own CLAUDE.md, so keep this file about the platform-service only. The system-wide decisions are in `../adr.md` and the requirements in `../spec.md`, which uses F, X and NF ids.

## Working rule

Build only what has been explicitly decided below. Do not add libraries, endpoints or feature code based on assumptions. If something is listed under *Open questions* or conflicts with the spec or the ADRs, ask first.

Everything in the repo is in English. Write comments and other text for people in plain sentences, with as few parentheses, dashes, semicolons and colons as possible and without needless numbering.

## Commands

- `dotnet build` builds the solution, and warnings are errors.
- `dotnet test` runs the unit and integration tests with xUnit v3 on Microsoft Testing Platform, which `global.json` selects. No database is needed.
- `dotnet run --project src/PlatformService.Api` starts the service on http://localhost:5080. It needs `ConnectionStrings:Platform`, and in Development Scalar is at `/scalar`.
- `dotnet user-secrets set "ConnectionStrings:Platform" "<connection string>" --project src/PlatformService.Api` sets the local connection string.
- `docker build -t platform-service .` builds the image, and `docker buildx build --platform linux/amd64,linux/arm64 -t platform-service .` builds it for both architectures.

## File structure

```
platform-service/
├── PlatformService.slnx
├── global.json                  # .NET 10 SDK with rollForward latestFeature, and Microsoft Testing Platform for dotnet test
├── Directory.Build.props        # net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors
├── Directory.Packages.props     # Central Package Management with every package version
├── .editorconfig, .gitignore, .dockerignore
├── Dockerfile                   # multi-stage build for amd64 and arm64, chiseled non-root runtime on port 8080
├── src/
│   ├── PlatformService.Api/                  # presentation and composition root
│   │   ├── Program.cs                        # DI calls and the middleware order, which is the request path
│   │   ├── DependencyInjection.cs            # AddPresentation registers forwarded headers, ProblemDetails, auth, MVC, JSON and OpenAPI
│   │   ├── Authentication/                   # Gateway scheme from x-user-id and x-user-name, and the ICurrentUser implementation
│   │   ├── Controllers/UsersController.cs    # GET /api/users/me
│   │   ├── Correlation/RequestCorrelation.cs # puts x-request-id into the CorrelationId logging scope
│   │   ├── ErrorHandling/                    # IExceptionHandler that turns exceptions into ProblemDetails
│   │   ├── OpenApi/                          # x-user-* headers as security schemes for Scalar
│   │   ├── appsettings.json                  # logging and ForwardedHeaders:KnownNetworks
│   │   └── Properties/launchSettings.json    # single http profile on http://localhost:5080
│   ├── PlatformService.Application/          # use cases, DTOs, ports and validators
│   │   ├── DependencyInjection.cs            # AddApplication registers services, validators and TimeProvider
│   │   ├── Abstractions/ICurrentUser.cs      # port for the signed-in user
│   │   ├── Common/Exceptions/                # NotFoundException, ConflictException, ForbiddenException
│   │   └── Users/                            # IUserService, UserService, CurrentUserDto
│   ├── PlatformService.Domain/               # entities and domain rules, empty for now
│   └── PlatformService.Infrastructure/       # EF Core and the port implementations
│       ├── DependencyInjection.cs            # AddInfrastructure registers the DbContext, its options and the ready check
│       ├── HealthCheckTags.cs                # the ready tag
│       └── Persistence/                      # PlatformDbContext without DbSets yet, and DatabaseOptions
└── tests/
    ├── PlatformService.UnitTests/            # Application logic with hand-written fakes
    └── PlatformService.IntegrationTests/     # WebApplicationFactory<Program> and the test-only ErrorTestController
```

## Decisions made

**Solution and layers**
- Clean Architecture in four projects. The project references enforce the dependency rule.
  - **Domain** references nothing and has no NuGet packages.
  - **Application** references Domain and uses `FluentValidation` and `FluentValidation.DependencyInjectionExtensions`. It must not depend on ASP.NET Core or EF Core.
  - **Infrastructure** references Application and uses `Npgsql.EntityFrameworkCore.PostgreSQL`, `EFCore.NamingConventions` and `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`.
  - **Api** references Application and Infrastructure and uses `Microsoft.AspNetCore.OpenApi` and `Scalar.AspNetCore`.
- Each layer has one DI extension method, which are `AddApplication`, `AddInfrastructure` and `AddPresentation`. The last two take the configuration. `Program.cs` only calls these and sets up the middleware.
- Package versions are the latest stable ones that support net10.0, pinned in `Directory.Packages.props`. The 11.0.x Microsoft packages target net11.0 only, so they stay on 10.0.x. The tests use only `xunit.v3` and `Microsoft.AspNetCore.Mvc.Testing`.
- Implementations are `internal sealed`. Interfaces, DTOs and exceptions are public. `InternalsVisibleTo` opens the Application to the unit tests.

**Endpoints and use cases**
- Controllers use the `ApiController` attribute and attribute routing under `api/`. A controller is thin. It passes the request to an Application service and returns the DTO.
- Every action has a `ProducesResponseType` attribute with the content type, and its errors are documented as `ProblemDetails` in `application/problem+json`, so the OpenAPI document is accurate.
- Each feature has one use case interface and one implementation, for example `IUserService` and `UserService` in `Application/Users/`. There is no mediator library.
- Mapping is written by hand, without AutoMapper.
- Repository and Unit of Work have no code yet, because there is no entity. Each aggregate gets one repository interface in the Application, implemented in the Infrastructure. The `PlatformDbContext` is the Unit of Work. There is no generic repository.

**Error handling**
- A single `IExceptionHandler` in `ErrorHandling/ProblemDetailsExceptionHandler.cs` turns exceptions into RFC 9457 ProblemDetails.
- The Application exceptions are in `Application/Common/Exceptions/`. `NotFoundException` becomes 404, `ConflictException` 409 and `ForbiddenException` 403. Their message is the `detail`, so write it for the client.
- FluentValidation's `ValidationException` becomes a 400 `ValidationProblemDetails`. Its `errors` are grouped by property with camelCase keys such as `address.postalCode`.
- Any other exception becomes a 500 without details or a stack trace. In Development the `detail` shows the exception. 5xx errors are logged at Error level.
- `AddProblemDetails` and `UseStatusCodePages` turn the 401, 403 and 404 responses without a body into ProblemDetails too. Every ProblemDetails has `traceId` and `correlationId`.
- Error messages are always in English, whatever the culture of the server. Write custom messages in English. `AddApplication` turns off the localization of FluentValidation.

**The request path in middleware order**
- **ForwardedHeaders** reads `X-Forwarded-For` and `X-Forwarded-Proto` only from the networks in `ForwardedHeaders:KnownNetworks`. This CIDR list is in `appsettings.json` with the private ranges and must not be empty. TLS ends at the gateway, so there is no `UseHttpsRedirection`, HSTS, CORS or rate limiting.
- **The exception handler and the status code pages** come next, as described under error handling.
- **Correlation** puts the gateway's `x-request-id` into the logging scope as `CorrelationId` and into the ProblemDetails as `correlationId`. If the header is missing, `TraceIdentifier` is used. The name is not `RequestId`, because ASP.NET Core's own logging scope already uses that for the `TraceIdentifier`.
- **Authentication** uses the Gateway scheme, which reads only `x-user-id` and `x-user-name`. Envoy strips only the headers in its `claim_to_headers` list, so no other `x-user-*` header is trusted.
  - Without `x-user-id` the result is `NoResult`.
  - If `x-user-id` is not a GUID or `x-user-name` is empty, the result is `Fail` with a Warning log.
  - Otherwise the principal gets the GUID as `sub` and `NameIdentifier`, and the user name as `name`.
  - The user id is a UUID v7 according to ADR-4, but the handler only checks that it is a GUID.
- **Authorization** has the fallback policy `RequireAuthenticatedUser`, like Envoy on `/api/*`. Anonymous access is allowed on `/health/*` and, in Development, on the OpenAPI document and Scalar. The future `AuthController` will allow it too.
- **The controller** calls an Application service, which reaches the Infrastructure through its ports.
  - The Application gets the user from the `ICurrentUser` port with `IsAuthenticated`, `UserId` and `UserName`. Without authentication the last two throw `InvalidOperationException`.
  - The Api implements it with `IHttpContextAccessor`. The Application never sees `HttpContext`.

**Validation**
- Validation uses FluentValidation in the Application. Every command or request type gets its own `AbstractValidator<T>` next to it.
- The first step of a service is the validation with `await validator.ValidateAndThrowAsync(command, ct)`. `AddApplication` registers every validator of the assembly.
- Request DTOs have no DataAnnotations. `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes` is true, so the automatic 400 response of `ApiController` only covers binding and format errors such as malformed JSON or a wrong type. All content rules are in validators.

**Endpoints**
- `GET /api/users/me` goes from `UsersController` to `IUserService.GetCurrentAsync` and returns 200 with `id` and `userName` from `ICurrentUser`. It does not read the database yet. Without the headers it returns a 401 ProblemDetails. The endpoint stays later too, because the access token is in an HttpOnly cookie and this is how the frontend learns who is signed in.
- `GET /health/live` returns 200 and checks no dependencies. `GET /health/ready` checks the database through the checks tagged `ready` and returns 200 or 503. Both allow anonymous access. They are not under `/api`, so the gateway does not expose them.

**Persistence**
- `Infrastructure/Persistence/PlatformDbContext.cs` has no `DbSet` yet. `OnModelCreating` collects the entity configurations with `ApplyConfigurationsFromAssembly`. Put one `IEntityTypeConfiguration<T>` per entity under `Persistence/`.
- `AddInfrastructure` sets up `UseNpgsql` with `ConnectionStrings:Platform` and `UseSnakeCaseNamingConvention`, and adds `AddDbContextCheck<PlatformDbContext>` with the `ready` tag. The Api knows only the tag, and the DbContext is internal.
- `ConnectionStrings:Platform` is required. It is read through options with `ValidateOnStart`, so a missing value stops the app at startup, and configuration that `WebApplicationFactory` adds later still applies. If the database is down, the app still starts and only `/health/ready` returns 503.
- There are no migrations yet. They come with the first entity, together with the migration strategy.

**Configuration and running**
- Configuration comes from `appsettings.json` and environment variables such as `ConnectionStrings__Platform`. Required settings have no built-in defaults, so a missing one stops the app at startup, as in the game-service. Secrets never go into the repo. Locally use `dotnet user-secrets`.
- In the container the service listens on port 8080 through `ASPNETCORE_HTTP_PORTS`. Locally it uses http://localhost:5080, because port 8080 belongs to the gateway according to the `.env.example` of the frontend.
- Logging uses the built-in `ILogger`. Outside Development the console logs JSON with scopes and UTC timestamps. Log methods are source generated with `LoggerMessage`.
- JSON uses System.Text.Json with camelCase and enums as strings, configured both for MVC and for the minimal API JSON options. Timestamps are `DateTimeOffset` values in UTC. Time-dependent logic takes a `TimeProvider`, registered as `TimeProvider.System`.

**OpenAPI and Scalar in Development**
- `AddOpenApi`, `MapOpenApi` at `/openapi/v1.json` and `MapScalarApiReference` at `/scalar`.
- A document transformer adds `x-user-id` and `x-user-name` as API key security schemes that the whole document requires together. Scalar can then send requests as a signed-in user. When an anonymous controller such as `AuthController` arrives, its operations need an exemption.

**Docker**
- The multi-stage `Dockerfile` builds with `sdk:10.0` using `--platform=$BUILDPLATFORM` and `-a $TARGETARCH`. It restores in a separate layer from the csproj and `Directory.*.props` files.
- The runtime image is `aspnet:10.0-noble-chiseled` for amd64 and arm64. It runs as a non-root user through `$APP_UID` and exposes port 8080.
- The chiseled image has no shell, so there is no `HEALTHCHECK`. Health is checked from the infra side.
- In the image, add `GSS Encryption Mode=Disable` to the connection string. The image has no Kerberos library, and without this setting Npgsql writes a harmless error to stderr.

**Tests**
- The tests use xUnit v3. `dotnet test` runs them on Microsoft Testing Platform, which the `test.runner` setting of `global.json` selects. Visual Studio Test Explorer runs them too.
- There is no assertion or mocking library, only the xUnit `Assert` and hand-written fakes. Pass `TestContext.Current.CancellationToken` to async calls, as the xUnit analyzer asks.
- `UnitTests` cover the Application logic, for example `UserService` with a fake `ICurrentUser`.
- `IntegrationTests` use `WebApplicationFactory<Program>` in the `Testing` environment with a test connection string. The `ErrorTestController` exists only in the test project, is added as an ApplicationPart and throws each mapped exception.

## Open questions

**Authentication**
- Registration, login, refresh, logout and password hashing. Should they use ASP.NET Core Identity or a custom users table?
- The token details that ADR-4 leaves open. These are the signing algorithm, the lifetimes of the access and refresh tokens, revocation, and whether the gateway gets the key from a JWKS endpoint or a key file.
- Non-ASCII user names will be allowed, but they cannot travel in `x-user-name` as they are. We checked that Kestrel rejects non-ASCII header values with 400 by default, and Node in the game-service reads header bytes as Latin-1. When login is built, either let Kestrel accept UTF-8 through `KestrelServerOptions.RequestHeaderEncodingSelector` and check that Envoy passes it through unchanged, or put an ASCII-safe user name into the claim that Envoy copies, for example a percent-encoded one. This changes the gateway contract of ADR-4.

**Persistence**
- The entities, the first migration and the migration strategy, which is either `MigrateAsync` at startup or a migration bundle.
- Where the UUID v7 is generated, in the application or with the PostgreSQL function `uuidv7`. The byte order of `Guid.CreateVersion7` in .NET is debated, so with the first entity check that the ids are stored in time order in a `uuid` column.

**Other services and contracts**
- Communication with other services, for example when the game-service hands over a finished game or when Élő ratings are updated.
- How the REST contract is published to the frontend, where the OpenAPI file lives and how TypeScript types are generated. This is an open question of ADR-2.
- Who owns `/api/games/*`. The game-service README and ADR-3 disagree, so do not create such endpoints for now.

**Infra**
- The docker compose setup, the Envoy configuration and CI, in the `infra` repo or later.

## Next steps

- Decide the authentication questions, then add the users table, password hashing and an `AuthController` with anonymous access under `/api/auth/*` for register, login, refresh and logout.
- With the first entity, add the first migration, decide the migration strategy and check how the UUID v7 values are stored.
- Build the game history of F-6 and NF-3 once the handover from the game-service and the owner of the games routes are agreed.
- Wire the service into `infra` with a compose service and its database user, the Envoy route with `claim_to_headers`, and health checks against `/health/live` and `/health/ready`.
- Agree how the OpenAPI contract is published to the frontend.
