# PS-ADR-12: Access tokens and sessions

**Related:** F-1, NF-1, ADR-3, ADR-4, [PS-ADR-1](clean-architecture.md), [PS-ADR-2](http-api.md), [PS-ADR-5](error-handling.md), [PS-ADR-7](persistence.md), [PS-ADR-8](configuration-and-logging.md), [PS-ADR-11](accounts.md)

## Context

ADR-4 decides the tokens, their lifetimes and their cookies. The service has to sign the access tokens with the key that compose mounts, so that the gateway accepts them. It also has to store the refresh tokens, so that a later refresh can rotate them and detect their reuse.

## Decision

- **Signing:** `JsonWebTokenHandler` of `Microsoft.IdentityModel.JsonWebTokens` in the Infrastructure signs the access token with ES256. The Application asks for it through the `IAccessTokenIssuer` port.
- **Key:** the service reads the PEM file at `Jwt:SigningKeyPath`. At startup it checks that the setting is set and that the file holds a private key on the P-256 curve, and it stops otherwise.
- **Header and claims:** the header has `alg`, `typ` and `kid`, and `kid` is the JWK thumbprint of RFC 7638, which the service computes from its key. The payload has exactly the claims of ADR-4, `iss`, `aud`, `sub`, `preferred_username`, `iat` and `exp`, without `nbf`. The issuer `platform-service` and the audience `chess-platform` are constants. The token is valid for 15 minutes from the time of the `TimeProvider` of PS-ADR-8.
- **Refresh token:** 32 random bytes from `RandomNumberGenerator` in base64url. The database stores only its SHA-256 hash, with a unique index. It is valid for 7 days from its issue.
- **Sessions:** the `Session` aggregate of the Domain owns its refresh tokens, and its repository is `ISessionRepository`. Registration and sign-in start a new session with its first refresh token.
- **Tables:** `sessions` holds the user and the start of each session, and `refresh_tokens` holds the session, the hash, the issue and the expiry of each token. Deleting a user deletes its sessions, and deleting a session deletes its tokens.
- **Commit:** registration saves the user and its session in one commit, as PS-ADR-7 and PS-ADR-11 describe.
- **Cookies:** `AuthController` sets the two cookies of ADR-4 from the result of the use case. Each cookie gets the lifetime of its token as `Max-Age`.
- **Wrong credentials:** a sign-in with an unknown user name or a wrong password throws `UnauthorizedException`, which becomes the 401 of the contract with the same `detail` for both cases (PS-ADR-5).

## Rationale

- `JsonWebTokenHandler` is the current JWT handler of Microsoft, which replaces `JwtSecurityTokenHandler`. It writes the ECDSA signature in the format of JWS and computes the JWK thumbprint, so the service needs no cryptographic code of its own.
- A `kid` computed from the key needs no setting that could differ from the JWKS of the gateway, as ADR-4 decides.
- A missing or broken key stops the service at startup with an error that names the setting, as the conventions require, instead of failing the first sign-in.
- ADR-4 lists the claims, and the gateway checks the expiry. A token is valid from its issue, so `nbf` would add nothing.
- The issuer and the audience are part of ADR-4 and are fixed in the configuration of the gateway too, so they do not vary between environments.
- 32 random bytes are 256 bits, which nobody can guess. So a fast hash without a salt is enough, unlike for a password, and the token is found by the unique index of its hash. A copy of the database holds no usable token.
- The rotation, the grace window and the reuse detection of ADR-4 are rules of a session, so the aggregate keeps them in the Domain, where unit tests can check them without a database.
- With a table of its own, a session is revoked in one row, and the refresh tokens of the session point to it.

## Rejected alternatives

- **`JwtSecurityTokenHandler` of `System.IdentityModel.Tokens.Jwt`:** the older handler, which `JsonWebTokenHandler` replaces.
- **Signing by hand with `ECDsa`:** no package, but own code in a security-critical place.
- **The issuer and the audience as settings:** they would be in the configuration of the service and of the gateway, and the two could differ.
- **The refresh token itself in the database:** a copy of the database would hold usable tokens.
- **A password hash such as PBKDF2 for the refresh token:** a slow hash protects guessable values, and it would slow down every refresh.
- **One `refresh_tokens` table without sessions:** revoking a session would change every token of it.

## Consequences

- The service does not start without a valid key, so the integration tests create a key of their own.
- The hashes of the replaced tokens stay for the reuse detection until they expire, so an active session collects rows, and the expired ones need a periodic cleanup.
- To do: the rotation with the grace window, the reuse detection, the logout and the cleanup of the expired tokens.

## Sources

- RFC 7519, JSON Web Token: https://www.rfc-editor.org/rfc/rfc7519
- RFC 7518, ECDSA signatures with ES256: https://www.rfc-editor.org/rfc/rfc7518#section-3.4
- RFC 7638, JSON Web Key Thumbprint: https://www.rfc-editor.org/rfc/rfc7638
- `JsonWebTokenHandler`: https://learn.microsoft.com/dotnet/api/microsoft.identitymodel.jsonwebtokens.jsonwebtokenhandler
- `Microsoft.IdentityModel.JsonWebTokens` package: https://www.nuget.org/packages/Microsoft.IdentityModel.JsonWebTokens
