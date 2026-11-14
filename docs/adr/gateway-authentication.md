# PS-ADR-4: Users from the gateway headers

**Related:** F-1, NF-1, ADR-3, ADR-4, [PS-ADR-1](clean-architecture.md), [PS-ADR-3](request-pipeline.md)

## Decision

- **Authentication** uses the Gateway scheme, which builds the user from `x-user-id` and `x-user-name`.
  - Without `x-user-id` the result is `NoResult`.
  - If `x-user-id` is not a GUID or `x-user-name` is empty, the result is `Fail` with a Warning log.
  - Otherwise the principal gets the GUID as `sub` and `NameIdentifier`, and the user name as `name`.
  - The handler checks only that the user id is a GUID, not that it is a UUID v7.
- **Authorization** has the fallback policy `RequireAuthenticatedUser`. Anonymous access is allowed on `/health/*` and, in Development, on the OpenAPI document and Scalar. The future `AuthController` will allow it too.
- **Current user:** the Application gets the user from the `ICurrentUser` port with `IsAuthenticated`, `UserId` and `UserName`. Without authentication the last two throw `InvalidOperationException`. The Api implements the port with `IHttpContextAccessor`, and the Application never sees `HttpContext`.
