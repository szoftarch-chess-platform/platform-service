# PS-ADR-2: HTTP API and its documentation

**Related:** F-1, ADR-3, ADR-4, ADR-7, ADR-8, [PS-ADR-4](gateway-authentication.md), [PS-ADR-5](error-handling.md), [PS-ADR-7](persistence.md), [PS-ADR-12](tokens-and-sessions.md), [PS-ADR-13](game-history.md)

## Context

The API needs an accurate description that can be tried out during development.

## Decision

- **Controllers:** controllers use the `ApiController` attribute and attribute routing under `api/`. A controller is thin. It passes the request to an Application service and returns the DTO.
- **Response types:** every action has a `ProducesResponseType` attribute with the content type, and its errors are documented as `ProblemDetails` in `application/problem+json`, so the OpenAPI document is accurate.
- **JSON:** System.Text.Json with camelCase fields, enums as camelCase strings and without `null` values, as ADR-8 decides, configured both for MVC and for the minimal API JSON options. An enum is read only from its string, because a number could bind to a value that the enum does not have.
- **Contract:** the contract `platform-service.openapi.yaml` in the infra repo describes the API, and the code follows it (ADR-7).
- **OpenAPI and Scalar in Development:** `AddOpenApi`, `MapOpenApi` at `/openapi/v1.json` and `MapScalarApiReference` at `/scalar`. A document transformer adds `x-user-id` and `x-user-name` as API key security schemes that the whole document requires together, so Scalar can send requests as a signed-in user. An operation transformer gives every operation that allows anonymous access, such as those of `AuthController`, an empty security requirement, which the contract shows as `security: []`.
- **Auth endpoints:** `AuthController` and `SessionsController` allow anonymous access and share the path `api/auth` and the group `Auth` of the document. `POST /api/auth/register` answers 201 and `POST /api/auth/login` answers 200. Both go to `IAuthService` and return `id` and `userName` with the cookies of PS-ADR-12. `POST /api/auth/refresh` and `POST /api/auth/logout` go to `ISessionService`, read the refresh token from its cookie and answer 204, and a refresh that ends the session answers 401 as PS-ADR-12 describes.
- **`GET /api/users/me`:** it goes from `UsersController` to `IUserService.GetCurrentAsync` and returns 200 with `id` and `userName` from `ICurrentUser`. It does not read the database. The endpoint is needed, because the access token is in an HttpOnly cookie, and this is how the frontend learns who is signed in.
- **Game history:** `GamesController` serves `GET /api/games` with the query parameters `page` and `pageSize`, and `GET /api/games/{gameId}` without a route constraint. Both go to `IGameHistoryService` and answer 200, as PS-ADR-13 describes.
- **Health:** `GET /health/live` returns 200 and checks no dependencies. `GET /health/ready` checks the database through the checks tagged `ready` and returns 200 or 503. They are not under `/api`, so the gateway does not expose them.
