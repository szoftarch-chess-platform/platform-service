# PS-ADR-12: Access tokens and sessions

**Related:** F-1, NF-1, ADR-3, ADR-4, [PS-ADR-1](clean-architecture.md), [PS-ADR-2](http-api.md), [PS-ADR-5](error-handling.md), [PS-ADR-7](persistence.md), [PS-ADR-8](configuration-and-logging.md), [PS-ADR-10](testing.md), [PS-ADR-11](accounts.md)

## Context

ADR-4 decides the tokens, their lifetimes and their cookies, and the rotation of the refresh token with its grace window and its reuse detection. The service has to sign the access tokens with the key that compose mounts, so that the gateway accepts them. It also has to store the refresh tokens, so that a later refresh can rotate them and detect their reuse, even when two refreshes with the same token arrive at the same time.

## Decision

- **Signing:** `JsonWebTokenHandler` of `Microsoft.IdentityModel.JsonWebTokens` in the Infrastructure signs the access token with ES256. The Application asks for it through the `IAccessTokenIssuer` port.
- **Key:** the service reads the PEM file at `Jwt:SigningKeyPath`. At startup it checks that the setting is set and that the file holds a private key on the P-256 curve, and it stops otherwise.
- **Header and claims:** the header has `alg`, `typ` and `kid`, and `kid` is the JWK thumbprint of RFC 7638, which the service computes from its key. The payload has exactly the claims of ADR-4, `iss`, `aud`, `sub`, `preferred_username`, `iat` and `exp`, without `nbf`. The issuer `platform-service` and the audience `chess-platform` are constants. The token is valid for 15 minutes from the time of the `TimeProvider` of PS-ADR-8.
- **Refresh token:** 32 random bytes from `RandomNumberGenerator` in base64url. The database stores only its SHA-256 hash, with a unique index. It is valid for 7 days from its issue.
- **Sessions:** the `Session` aggregate of the Domain owns its refresh tokens and the rules of ADR-4, and its repository is `ISessionRepository`. Registration and sign-in start a new session with its first refresh token.
- **Tables:** `sessions` holds the user, the start and the revocation time of each session, and `refresh_tokens` holds the session, the hash, the issue, the expiry and the replacement time of each token. The expiry has an index for the cleanup. Deleting a user deletes its sessions, and deleting a session deletes its tokens.
- **Commit:** registration saves the user and its session in one commit, as PS-ADR-7 and PS-ADR-11 describe.
- **Cookies:** the controllers set the two cookies of ADR-4 from the result of the use case. Each cookie gets the lifetime of its token as `Max-Age`.
- **Wrong credentials:** a sign-in with an unknown user name or a wrong password throws `UnauthorizedException`, which becomes the 401 of the contract with the same `detail` for both cases (PS-ADR-5).
- **Use cases:** `IAuthService` registers and signs in, and `ISessionService` refreshes and signs out. `AuthController` and `SessionsController` share the path `api/auth`.
- **Refresh:** `SessionService` looks up the session by the hash of the token in the cookie, and `ISessionRepository` loads the session with that token only. The session checks the rules in this order. A revoked session or an expired token ends the refresh. A token that has not been replaced yet is replaced by a new one. A token that was replaced at most 10 seconds ago gets another new token next to the one that replaced it. A token that was replaced earlier revokes the session. Every new token is valid for 7 days, and the new access token gets the user name from `IUserAccounts`.
- **Parallel refreshes:** the replacement time is an EF Core concurrency token. So the update of a token only succeeds if the token has not been replaced yet, and it is in the same commit as the new token. Of two refreshes with the same token only one commits. The other gets `ConcurrencyConflictException` from `IUnitOfWork`, which also forgets the loaded data, and the use case tries once more. The second try finds the token replaced a moment ago and takes the grace window.
- **Revocation:** logout and a detected reuse set the revocation time of the session, and every token of a revoked session is refused. Logout revokes the session of the token in the cookie even when the token has been replaced or has expired, and without a known token it changes nothing.
- **Responses:** a refresh that ends the session returns a result instead of throwing, because the exception handler of PS-ADR-5 clears the headers of the response. `SessionsController` then deletes both cookies and answers 401 ProblemDetails with the same `detail` for every reason. Logout always deletes both cookies and answers 204.
- **Reuse log:** `SessionService` logs a detected reuse as a warning with the id of the session and of the user, never with the token. It uses `ILogger` with `LoggerMessage` as PS-ADR-8 describes.
- **Cleanup:** a `BackgroundService` in the Infrastructure runs at startup and then every hour with a `PeriodicTimer` on the `TimeProvider`. With `ExecuteDeleteAsync` it deletes the expired refresh tokens and the sessions that have no valid token left. A failed run is logged, and the next one follows on time.

## Rationale

- `JsonWebTokenHandler` is the current JWT handler of Microsoft, which replaces `JwtSecurityTokenHandler`. It writes the ECDSA signature in the format of JWS and computes the JWK thumbprint, so the service needs no cryptographic code of its own.
- A `kid` computed from the key needs no setting that could differ from the JWKS of the gateway, as ADR-4 decides.
- A missing or broken key stops the service at startup with an error that names the setting, as the conventions require, instead of failing the first sign-in.
- ADR-4 lists the claims, and the gateway checks the expiry. A token is valid from its issue, so `nbf` would add nothing.
- The issuer and the audience are part of ADR-4 and are fixed in the configuration of the gateway too, so they do not vary between environments.
- 32 random bytes are 256 bits, which nobody can guess. So a fast hash without a salt is enough, unlike for a password, and the token is found by the unique index of its hash. A copy of the database holds no usable token.
- The rotation, the grace window and the reuse detection of ADR-4 are rules of a session, so the aggregate keeps them in the Domain, where unit tests can check them without a database.
- With a table of its own, a session is revoked in one row, and the refresh tokens of the session point to it.
- Refresh and sign-out need neither the validators nor the sign-in of the accounts, so with use cases and controllers of their own every class has only the dependencies that it uses.
- The rules of a refresh need only the session and the token in the cookie. An active session collects a new token every 15 minutes for 7 days, so loading all of them would read hundreds of rows for nothing.
- The concurrency token makes EF Core write the condition `replaced_at IS NULL` into the update and check that it changed one row. PostgreSQL checks the condition again on the newest version of the row when a parallel update has just committed, so only one of two updates matches. The rule stays in the Domain, and the replacement and the new token are saved together or not at all.
- One retry is always enough, because the grace window only adds a token and changes no row that another refresh could change.
- A revocation changes only the row of the session, and a token is always checked together with its session. A rotation that runs at the same time as a revocation can at most add a token to the revoked session, and that token is refused like the others.
- The expiry is checked before the reuse, so the answer does not depend on whether the cleanup has deleted an expired token yet.
- The same `detail` for every reason tells nothing about the state of a token.
- A replaced token that comes back usually means that a token was stolen, which is why RFC 9700 asks for the revocation. The OWASP Logging Cheat Sheet counts the failures of the session management and the suspicious use of tokens among the security events that a service logs. `Microsoft.Extensions.Logging.Abstractions` holds only the interfaces of logging and depends neither on ASP.NET Core nor on EF Core, so PS-ADR-1 allows it in the Application.
- An expired token is refused by the rules anyway, so the cleanup only keeps the tables small and deletes no row that a rule still needs. Its statements are idempotent, so several instances can run them at the same time without a lock.
- A timed background task in a hosted service is the pattern of ASP.NET Core for periodic work, and with the `TimeProvider` its timer follows the clock of PS-ADR-8.
- Since .NET 6 an unhandled exception of a `BackgroundService` stops the whole service, so a run that fails, for example while the database is down, is caught and logged.

## Rejected alternatives

- **`JwtSecurityTokenHandler` of `System.IdentityModel.Tokens.Jwt`:** the older handler, which `JsonWebTokenHandler` replaces.
- **Signing by hand with `ECDsa`:** no package, but own code in a security-critical place.
- **The issuer and the audience as settings:** they would be in the configuration of the service and of the gateway, and the two could differ.
- **The refresh token itself in the database:** a copy of the database would hold usable tokens.
- **A password hash such as PBKDF2 for the refresh token:** a slow hash protects guessable values, and it would slow down every refresh.
- **One `refresh_tokens` table without sessions:** revoking a session would change every token of it.
- **A row lock with `SELECT ... FOR UPDATE`:** the second refresh would wait without a retry, but it needs raw SQL and an explicit transaction, which the single commit of PS-ADR-7 avoids, and EF Core has no LINQ operator for it.
- **A conditional `ExecuteUpdateAsync` outside the aggregate:** it runs before the commit, so without an explicit transaction it is not saved together with the new token, and the rule of the rotation would move into the repository.
- **Deleting the session on logout and on reuse:** a rotation at the same time would fail on the foreign key, and a token of a revoked session could not be told from an unknown one.
- **pg_cron in PostgreSQL:** the PostgreSQL image of the infra compose does not have the extension, and the cleanup would move into the infra repo.
- **Cleanup only at sign-in and refresh:** the sessions that nobody uses any more would never be deleted.
- **The reuse log in the controller:** the result would have to carry the ids of the session and of the user to the Api, and the controller would get logic of its own.

## Consequences

- The service does not start without a valid key, so the integration tests create a key of their own.
- The hashes of the replaced tokens stay for the reuse detection until they expire, and the cleanup deletes them then. A refresh in the grace window leaves two valid tokens in the session, as ADR-4 describes.
- A session ends at most 7 days after its last refresh, and the next cleanup deletes it.
- The tests replace the `TimeProvider` with a clock of their own to reach the grace window and the expiry. The integration tests share one database, so their hosts run without the cleanup, which would otherwise delete the rows of other tests (PS-ADR-10).

## Sources

- RFC 7519, JSON Web Token: https://www.rfc-editor.org/rfc/rfc7519
- RFC 7518, ECDSA signatures with ES256: https://www.rfc-editor.org/rfc/rfc7518#section-3.4
- RFC 7638, JSON Web Key Thumbprint: https://www.rfc-editor.org/rfc/rfc7638
- `JsonWebTokenHandler`: https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.jsonwebtokens.jsonwebtokenhandler
- `Microsoft.IdentityModel.JsonWebTokens` package: https://www.nuget.org/packages/Microsoft.IdentityModel.JsonWebTokens
- RFC 9700, refresh token protection: https://www.rfc-editor.org/rfc/rfc9700#section-4.14.2
- EF Core, handling concurrency conflicts: https://learn.microsoft.com/ef/core/saving/concurrency
- EF Core, filtered include: https://learn.microsoft.com/ef/core/querying/related-data/eager#filtered-include
- PostgreSQL 18, the Read Committed isolation level: https://www.postgresql.org/docs/18/transaction-iso.html#XACT-READ-COMMITTED
- OWASP Logging Cheat Sheet: https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html
- Background tasks with hosted services in ASP.NET Core: https://learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services
- Unhandled exceptions of a `BackgroundService` stop the host: https://learn.microsoft.com/dotnet/core/compatibility/core-libraries/6.0/hosting-exception-handling
- `PeriodicTimer` with a `TimeProvider`: https://learn.microsoft.com/dotnet/api/system.threading.periodictimer.-ctor
