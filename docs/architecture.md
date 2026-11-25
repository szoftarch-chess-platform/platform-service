# platform-service architecture

The diagrams of the service at the component level of the C4 model.

## Layers

The arrows are project references, so the compiler checks the dependency rule of [PS-ADR-1](adr/clean-architecture.md). The dashed arrows are the connections to PostgreSQL of [PS-ADR-7](adr/persistence.md) and to RabbitMQ of [PS-ADR-14](adr/messaging.md). The health probe of [PS-ADR-9](adr/container-image.md) is outside the layers and references no project. It asks the readiness endpoint over HTTP.

```mermaid
flowchart TD
    Api["<b>PlatformService.Api</b><br/>controllers, HTTP pipeline,<br/>composition root"]
    App["<b>PlatformService.Application</b><br/>use cases, DTOs, ports, validators"]
    Dom["<b>PlatformService.Domain</b><br/>entities, domain rules"]
    Infra["<b>PlatformService.Infrastructure</b><br/>EF Core, Identity, access tokens,<br/>consumer of the finished games,<br/>port implementations"]
    Probe["<b>PlatformService.HealthProbe</b><br/>health check of the container"]
    Pg[("PostgreSQL")]
    Mq[["RabbitMQ"]]

    Api --> App
    Api --> Infra
    Infra --> App
    App --> Dom
    Infra -.-> Pg
    Infra -.-> Mq
    Probe -.->|"GET /health/ready"| Api
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

## Game history

How `GET /api/games` of [PS-ADR-13](adr/game-history.md) lists the games of the signed-in user. `GET /api/games/{gameId}` takes the same path with `GetAsync` and `FindAsync`, and it answers 404 for a game that does not exist and for a game that the user did not play.

```mermaid
sequenceDiagram
    participant Envoy
    participant Ctrl as GamesController
    participant Svc as GameHistoryService
    participant CU as ICurrentUser
    participant Repo as IGameRepository
    participant Pg as PostgreSQL

    Envoy->>Ctrl: GET /api/games with page and pageSize
    Ctrl->>Svc: ListAsync
    Svc->>Svc: the validator checks the page and its size
    Svc->>CU: UserId
    Svc->>Repo: CountByPlayerAsync
    Repo->>Pg: count the games with the user as white or black
    opt the page is not after the last one
        Svc->>Repo: ListByPlayerAsync
        Repo->>Pg: the games of the page by ended_at and then by id, both descending
    end
    Svc-->>Ctrl: GamePageDto
    Ctrl-->>Envoy: 200
```

## Queues of the finished games

The exchanges and the queues of [PS-ADR-14](adr/messaging.md) in RabbitMQ. The game-service declares `game.events`, and the service declares the rest. The policy of the infra repo, which ADR-6 decides, adds the dead-letter exchange, the delivery limit and the delayed retry to `platform.game-finished`.

```mermaid
flowchart LR
    events{{"game.events<br/>topic exchange"}}
    queue[["platform.game-finished<br/>quorum queue"]]
    consumer["GameFinishedConsumer"]
    dlx{{"platform.game-finished.dlx<br/>fanout exchange"}}
    dlq[["platform.game-finished.dlq<br/>quorum queue"]]

    events -->|"game.finished"| queue
    queue -->|"one message at a time"| consumer
    queue -.->|"rejected without requeue,<br/>or past the delivery limit of 20"| dlx
    dlx --> dlq
```

## Recording a finished game

How the consumer of [PS-ADR-14](adr/messaging.md) records a game through the use case of [PS-ADR-13](adr/game-history.md), and how it answers the broker. The answer follows the commit, so a game is acknowledged only once it is stored.

```mermaid
sequenceDiagram
    participant Mq as RabbitMQ
    participant Con as GameFinishedConsumer
    participant Han as GameFinishedHandler
    participant Svc as FinishedGameService
    participant Repo as IGameRepository
    participant Uow as IUnitOfWork
    participant Pg as PostgreSQL

    Mq->>Con: a message of game.finished.v1
    Con->>Han: HandleAsync with the body
    alt not the JSON of the envelope, or another type
        Con-->>Mq: basic.reject without requeue
    else an event of game.finished.v1
        Han->>Svc: RecordAsync with the command of the event
        Svc->>Svc: the validator checks the game
        Svc->>Repo: IsRecordedAsync
        Repo->>Pg: is the game or its event stored already
        opt not stored yet
            Svc->>Repo: Add with the id of the event
            Svc->>Uow: SaveChangesAsync
            Uow->>Pg: the game in one commit
        end
        alt recorded now or before
            Con-->>Mq: basic.ack
        else the validator refuses the game
            Con-->>Mq: basic.reject without requeue
        else a transient error of the database
            Con-->>Mq: basic.nack with requeue
        else any other error
            Con-->>Mq: basic.reject with requeue
        end
    end
```
