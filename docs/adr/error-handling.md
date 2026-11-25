# PS-ADR-5: Errors as ProblemDetails

**Related:** [PS-ADR-3](request-pipeline.md), [PS-ADR-6](validation.md), [PS-ADR-11](accounts.md), [PS-ADR-12](tokens-and-sessions.md), [PS-ADR-13](game-history.md)

## Context

Errors come from several places, such as the exceptions of the use cases, failed validation and failed authentication, and the clients need one format for all of them.

## Decision

- A single `IExceptionHandler` in `ErrorHandling/ProblemDetailsExceptionHandler.cs` turns exceptions into RFC 9457 ProblemDetails in `application/problem+json`.
- The Application exceptions are in `Application/Common/Exceptions/`. Their message becomes the `detail`, so write it for the client.
- `AddProblemDetails` and `UseStatusCodePages` turn the 401, 403 and 404 responses without a body into ProblemDetails too.
- Every ProblemDetails has a `traceId`, and a `correlationId` with the `CorrelationId` of the request.
- Causes and status codes:

| Cause | Status |
|---|---|
| `NotFoundException` | 404, such as a game that does not exist or that the user did not play (PS-ADR-13) |
| `ConflictException` | 409, with `errors` under camelCase keys when the exception names the conflicting fields, such as a taken user name of PS-ADR-11 |
| `UnauthorizedException` | 401, such as a wrong user name or password at sign-in (PS-ADR-12) |
| A refresh that ends the session | 401 from `SessionsController` without an exception, so that the deletion of the cookies stays in the response (PS-ADR-12) |
| `ForbiddenException` or a policy that denies access | 403 |
| FluentValidation `ValidationException` | 400 `ValidationProblemDetails`, with `errors` grouped by property under camelCase keys such as `address.postalCode` |
| Malformed JSON, or a wrong type in the body, the query or the route, such as a game id that is not a UUID | 400 from `ApiController` |
| Missing or invalid identity headers | 401 |
| Anything else | 500 without details or a stack trace, while in Development the `detail` shows the exception |

- 5xx errors are logged at Error level.
- Error messages are always in English, whatever the culture of the server. Write custom messages in English. `AddApplication` turns off the localization of FluentValidation.
