# platform-service architecture

The diagrams of the service at the component level of the C4 model.

## Layers

The arrows are project references, so the compiler checks the dependency rule of [PS-ADR-1](adr/clean-architecture.md).

```mermaid
flowchart TD
    Api["<b>PlatformService.Api</b><br/>controllers, HTTP pipeline,<br/>composition root"]
    App["<b>PlatformService.Application</b><br/>use cases, DTOs, ports, validators"]
    Dom["<b>PlatformService.Domain</b><br/>entities, domain rules"]
    Infra["<b>PlatformService.Infrastructure</b><br/>EF Core, Identity, access tokens,<br/>port implementations"]
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

## Registration

How `POST /api/auth/register` of [PS-ADR-2](adr/http-api.md) creates the account of [PS-ADR-11](adr/accounts.md) and its session of [PS-ADR-12](adr/tokens-and-sessions.md), and saves both in the one commit of [PS-ADR-7](adr/persistence.md). Sign-in takes the same path with `FindByCredentialsAsync` instead of `CreateAsync`.

```mermaid
sequenceDiagram
    participant Envoy
    participant Ctrl as AuthController
    participant Svc as AuthService
    participant Val as Validator
    participant Acc as IUserAccounts
    participant Ses as Session
    participant Repo as ISessionRepository
    participant Tok as IAccessTokenIssuer
    participant Uow as IUnitOfWork
    participant Pg as PostgreSQL

    Envoy->>Ctrl: POST /api/auth/register
    Ctrl->>Svc: RegisterAsync
    Svc->>Val: ValidateAndThrowAsync
    Svc->>Acc: CreateAsync
    Acc->>Pg: is the user name or the email address taken
    Svc->>Ses: Start with the hash of the refresh token
    Svc->>Repo: Add
    Svc->>Tok: Issue
    Svc->>Uow: SaveChangesAsync
    Uow->>Pg: the user, the session and its token in one commit
    Svc-->>Ctrl: user and tokens
    Ctrl-->>Envoy: 201 with the two cookies
```

## Refresh

How `POST /api/auth/refresh` of [PS-ADR-2](adr/http-api.md) applies the rotation, the grace window and the reuse detection of [PS-ADR-12](adr/tokens-and-sessions.md), and how the concurrency token of [PS-ADR-7](adr/persistence.md) lets only one of two parallel refreshes replace the token. Logout takes the same path up to the session, revokes it and answers 204.

```mermaid
sequenceDiagram
    participant Envoy
    participant Ctrl as SessionsController
    participant Svc as SessionService
    participant Repo as ISessionRepository
    participant Ses as Session
    participant Acc as IUserAccounts
    participant Tok as IAccessTokenIssuer
    participant Uow as IUnitOfWork
    participant Pg as PostgreSQL

    Envoy->>Ctrl: POST /api/auth/refresh with the refresh cookie
    Ctrl->>Svc: RefreshAsync
    Svc->>Repo: FindByRefreshTokenAsync with the hash
    Repo->>Pg: the session with this one token
    Svc->>Ses: Refresh
    alt not replaced yet, or replaced at most 10 seconds ago
        Svc->>Acc: FindByIdAsync
        Svc->>Tok: Issue
        Svc->>Uow: SaveChangesAsync
        Uow->>Pg: the replacement only where replaced_at is still null, and the new token
        opt a parallel refresh replaced the token first
            Uow-->>Svc: ConcurrencyConflictException
            Svc->>Repo: FindByRefreshTokenAsync once more, which ends in the grace window
        end
        Svc-->>Ctrl: new tokens
        Ctrl-->>Envoy: 204 with the two cookies
    else replaced earlier
        Svc->>Uow: SaveChangesAsync with the revoked session
        Svc-->>Ctrl: session ended
        Ctrl-->>Envoy: 401 that deletes the two cookies
    else unknown, expired or revoked
        Svc-->>Ctrl: session ended
        Ctrl-->>Envoy: 401 that deletes the two cookies
    end
```
