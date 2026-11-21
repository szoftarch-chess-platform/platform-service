# PS-ADR-10: Tests with xUnit v3, hand-written fakes and Testcontainers

**Related:** [PS-ADR-1](clean-architecture.md), [PS-ADR-5](error-handling.md), [PS-ADR-7](persistence.md), [PS-ADR-11](accounts.md)

## Context

The Application logic and the HTTP behavior of the service need automated tests. The accounts and the data of the service rely on PostgreSQL itself, such as its unique indexes and its migrations, which a fake cannot show.

## Decision

- The tests use xUnit v3. `dotnet test` runs them on Microsoft Testing Platform, which the `test.runner` setting of `global.json` selects.
- The test projects use only `xunit.v3`, `Microsoft.AspNetCore.Mvc.Testing` and `Testcontainers.PostgreSql`. There is no assertion or mocking library, only the xUnit `Assert` and hand-written fakes. Pass `TestContext.Current.CancellationToken` to async calls, as the xUnit analyzer asks.
- `UnitTests` cover the Application logic, for example `UserService` with a fake `ICurrentUser`. They need no Docker.
- `IntegrationTests` run the whole app in memory with `WebApplicationFactory<Program>` in the `Testing` environment. They cover authentication, health, the JSON conventions, the mapping of exceptions to ProblemDetails and the storage in PostgreSQL. The test project adds its own controllers as an ApplicationPart. The `ErrorTestController` throws each mapped exception, and the `JsonTestController` answers with an enum and a property without a value.
- An assembly fixture starts one PostgreSQL container for the integration tests with Testcontainers, from the image of the infra compose, and applies the migrations of PS-ADR-7. The tests share the database, so each one creates its own data with new names.

## Rationale

- A real PostgreSQL shows what the service relies on, such as the unique indexes of PS-ADR-11, the order of the UUID v7 ids of PS-ADR-7 and a model without a missing migration. The in-memory provider of EF Core and SQLite behave differently.
- One container for the assembly keeps the tests fast, and new names per test let them run in parallel without cleaning up.

## Consequences

- Docker has to run for the integration tests, locally and in the CI.
