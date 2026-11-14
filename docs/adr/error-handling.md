# PS-ADR-5: Errors as ProblemDetails

**Related:** [PS-ADR-3](request-pipeline.md), [PS-ADR-6](validation.md)

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
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| `ForbiddenException` or a policy that denies access | 403 |
| FluentValidation `ValidationException` | 400 `ValidationProblemDetails`, with `errors` grouped by property under camelCase keys such as `address.postalCode` |
| Malformed JSON or a wrong type in the request | 400 from `ApiController` |
| Missing or invalid identity headers | 401 |
| Anything else | 500 without details or a stack trace, while in Development the `detail` shows the exception |

- 5xx errors are logged at Error level.
- Error messages are always in English, whatever the culture of the server. Write custom messages in English. `AddApplication` turns off the localization of FluentValidation.
