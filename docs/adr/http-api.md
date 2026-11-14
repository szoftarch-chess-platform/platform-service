# PS-ADR-2: HTTP API and its documentation

**Related:** F-1, ADR-3, ADR-4, [PS-ADR-4](gateway-authentication.md), [PS-ADR-5](error-handling.md), [PS-ADR-7](persistence.md)

## Context

The API needs an accurate description that can be tried out during development.

## Decision

- **Controllers:** controllers use the `ApiController` attribute and attribute routing under `api/`. A controller is thin. It passes the request to an Application service and returns the DTO.
- **Response types:** every action has a `ProducesResponseType` attribute with the content type, and its errors are documented as `ProblemDetails` in `application/problem+json`, so the OpenAPI document is accurate.
- **JSON:** System.Text.Json with camelCase and enums as strings, configured both for MVC and for the minimal API JSON options.
- **OpenAPI and Scalar in Development:** `AddOpenApi`, `MapOpenApi` at `/openapi/v1.json` and `MapScalarApiReference` at `/scalar`. A document transformer adds `x-user-id` and `x-user-name` as API key security schemes that the whole document requires together, so Scalar can send requests as a signed-in user. When an anonymous controller such as `AuthController` arrives, its operations need an exemption.
- **`GET /api/users/me`:** it goes from `UsersController` to `IUserService.GetCurrentAsync` and returns 200 with `id` and `userName` from `ICurrentUser`. It does not read the database yet. The endpoint stays later too, because the access token is in an HttpOnly cookie, and this is how the frontend learns who is signed in.
- **Health:** `GET /health/live` returns 200 and checks no dependencies. `GET /health/ready` checks the database through the checks tagged `ready` and returns 200 or 503. They are not under `/api`, so the gateway does not expose them.
