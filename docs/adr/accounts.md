# PS-ADR-11: User accounts with ASP.NET Core Identity

**Related:** F-1, NF-1, ADR-3, ADR-4, [PS-ADR-1](clean-architecture.md), [PS-ADR-4](gateway-authentication.md), [PS-ADR-5](error-handling.md), [PS-ADR-6](validation.md), [PS-ADR-7](persistence.md), [PS-ADR-12](tokens-and-sessions.md)

## Context

ADR-4 decides the tokens and the rules of the accounts, and NF-1 requires that the passwords are stored hashed. The service needs a store of the users with password hashing, and with user names and email addresses that are unique regardless of case.

## Decision

- The accounts use ASP.NET Core Identity through `AddIdentityCore` and its EF Core stores in the database of the service. The package is `Microsoft.AspNetCore.Identity.EntityFrameworkCore` in the Infrastructure. The DbContext derives from `IdentityUserContext`, because the service has no roles.
- The user type `PlatformUser` and `UserManager` stay in the Infrastructure. The Application reaches them through the `IUserAccounts` port, as PS-ADR-1 requires. The port creates an account and finds the account of a user name with its password, and it returns a `UserAccount` with the id and the user name. The Domain has no user.
- The key of the user type is a `Guid`, which holds the UUID v7 of ADR-4.
- The store of Identity does not save on its own, so a new user goes into the database in the one commit of the use case, together with its session (PS-ADR-7, PS-ADR-12).
- A taken user name or email address throws a `ConflictException` that names the taken fields, which becomes a 409 (PS-ADR-5). Identity finds it before the commit, and the unique indexes find it at the commit when two registrations run at the same time.
- The options of Identity follow ADR-4. The user name may contain letters, digits, `_` and `-`, the email address is unique, and the password needs 8 characters without composition rules. The validators of PS-ADR-6 check the other limits of length.
- `PasswordHasher` of Identity hashes the passwords in its default V3 format.
- Lockout is turned off.
- The cookies, the tokens and the endpoints of Identity, such as `MapIdentityApi`, are not used. The service issues the tokens of ADR-4 itself.

## Rationale

- Identity normalizes the user names and the email addresses, so the uniqueness of ADR-4 regardless of case comes with it, and `UserManager` checks the password rules.
- The V3 format is PBKDF2 with HMAC-SHA512, a 128-bit salt and 100,000 iterations. The hash records its own parameters, so the old hashes still verify after a change of the iteration count.
- `AddIdentityCore` registers the user management without the cookie authentication of `AddIdentity`, which the service does not need, because the gateway checks the tokens (PS-ADR-4).
- The gateway limits the sign-in attempts of an IP address (ADR-3). A lockout per account would let anyone lock a user out by trying wrong passwords for that user name.
- The endpoints of Identity issue their own tokens and cookies, not the ES256 tokens with the rotating refresh token of ADR-4.
- The use cases need only the id and the user name of an account, which the user of Identity already holds.
- Without the save of the store, a registration cannot leave an account without a session behind, which would answer 500 and then 409 to the next attempt.

## Rejected alternatives

- **Own users table with own hashing:** no new package, but the hashing, the normalization and the uniqueness would be written by hand.
- **`MapIdentityApi`:** its tokens are not the ones of ADR-4.
- **A Domain entity for the business data of the users next to the user of Identity, with the same id:** a good split once the users have business data, such as an Élő rating, but they have none yet. The passwords would still need the port.

## Consequences

- The defaults of Identity differ from ADR-4. They require 6 characters with a digit, a lowercase letter, an uppercase letter and a symbol, and they allow `.`, `@` and `+` in user names. So every option is set explicitly.
- The model of Identity has a unique index on the normalized user name, but the index on the normalized email is not unique. The entity configuration makes it unique, so two parallel registrations cannot take the same address.
- The tables of Identity come with the first migration as `users`, `user_claims`, `user_logins` and `user_tokens`. Identity names them and their indexes explicitly, so the entity configurations give them the snake case names of PS-ADR-7.
- Every operation of Identity that changes a user needs the commit of its use case.
- When the users get their first business data, such as an Élő rating or friendships, the team decides again whether it goes into a Domain entity with the same id. The id and the user name stay in `PlatformUser` either way, and no business data goes into `PlatformUser` without that decision.

## Sources

- Configure ASP.NET Core Identity: https://learn.microsoft.com/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0
- `PasswordHasher` source: https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Extensions.Core/src/PasswordHasher.cs
