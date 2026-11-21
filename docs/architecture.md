# platform-service architecture

The diagrams of the service at the component level of the C4 model.

## Layers

The arrows are project references, so the compiler checks the dependency rule of [PS-ADR-1](adr/clean-architecture.md).

```mermaid
flowchart TD
    Api["<b>PlatformService.Api</b><br/>controllers, HTTP pipeline,<br/>composition root"]
    App["<b>PlatformService.Application</b><br/>use cases, DTOs, ports, validators"]
    Dom["<b>PlatformService.Domain</b><br/>entities, domain rules"]
    Infra["<b>PlatformService.Infrastructure</b><br/>EF Core, Identity,<br/>port implementations"]
    Pg[("PostgreSQL")]

    Api --> App
    Api --> Infra
    Infra --> App
    App --> Dom
    Infra -.-> Pg
```

## Middleware

The middleware of [`Program.cs`](../src/PlatformService.Api/Program.cs) in the order that [PS-ADR-3](adr/request-pipeline.md) decides.

```mermaid
flowchart LR
    gateway(["Envoy gateway"])
    fh["ForwardedHeaders"]
    eh["ExceptionHandler and StatusCodePages"]
    corr["Request correlation"]
    authn["Gateway authentication"]
    authz["Fallback authorization"]
    ctrl["Controller"]

    gateway --> fh --> eh --> corr --> authn --> authz --> ctrl
```

## Reading the current user

How `GET /api/users/me` of [PS-ADR-2](adr/http-api.md) passes the layers, with the authentication of [PS-ADR-4](adr/gateway-authentication.md).

```mermaid
sequenceDiagram
    participant Envoy
    participant AuthZ as Authorization
    participant Ctrl as UsersController
    participant Svc as IUserService
    participant CU as ICurrentUser

    Envoy->>AuthZ: GET /api/users/me
    alt no valid identity headers
        AuthZ-->>Envoy: 401
    else signed-in user
        AuthZ->>Ctrl: request
        Ctrl->>Svc: GetCurrentAsync
        Svc->>CU: UserId and UserName
        CU-->>Svc: values from the claims
        Svc-->>Ctrl: CurrentUserDto
        Ctrl-->>Envoy: 200
    end
```

## Planned write path

The path of the later use cases that change data. It is not implemented yet.

```mermaid
sequenceDiagram
    participant Svc as Service
    participant Val as Validator
    participant Dom as Domain
    participant Repo as Repository
    participant Db as DbContext
    participant Pg as PostgreSQL

    Svc->>Val: ValidateAndThrowAsync
    Svc->>Dom: apply the domain rules
    Svc->>Repo: load and save the aggregate
    Repo->>Db: SaveChangesAsync
    Db->>Pg: SQL
```
