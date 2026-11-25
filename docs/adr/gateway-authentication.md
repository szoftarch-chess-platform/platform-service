# PS-ADR-4: Users from the gateway headers

**Related:** F-1, NF-1, ADR-3, ADR-4, [PS-ADR-1](clean-architecture.md), [PS-ADR-3](request-pipeline.md)

## Decision

- **Authentication** uses the Gateway scheme, which builds the user from `x-user-id` and `x-user-name`.
  - The scheme is registered with `AddAuthenticationCore`, `AddWebEncoders` for the `UrlEncoder` of the handler and an `AuthenticationBuilder`. `AddAuthentication` is not used, because it also registers Data Protection for cookie and remote schemes, which the service does not have, and its key ring would start with warnings about keys that are neither persisted nor encrypted.
  - Without `x-user-id` the result is `NoResult`.
  - If `x-user-id` is not a GUID or `x-user-name` is empty, the result is `Fail` with a Warning log.
  - Otherwise the principal gets the GUID as `sub` and `NameIdentifier`, and the user name as `name`.
  - The handler checks only that the user id is a GUID, not that it is a UUID v7.
- **Authorization** has the fallback policy `RequireAuthenticatedUser`. Anonymous access is allowed on `/health/*` and, in Development, on the OpenAPI document and Scalar. `AuthController` and `SessionsController` allow it too, because registration, sign-in, refresh and sign-out come without an access token.
- **Current user:** the Application gets the user from the `ICurrentUser` port with `IsAuthenticated`, `UserId` and `UserName`. Without authentication the last two throw `InvalidOperationException`. The Api implements the port with `IHttpContextAccessor`, and the Application never sees `HttpContext`.
