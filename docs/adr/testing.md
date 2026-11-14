# PS-ADR-10: Tests with xUnit v3 and hand-written fakes

**Related:** [PS-ADR-1](clean-architecture.md), [PS-ADR-5](error-handling.md)

## Context

The Application logic and the HTTP behavior of the service need automated tests.

## Decision

- The tests use xUnit v3. `dotnet test` runs them on Microsoft Testing Platform, which the `test.runner` setting of `global.json` selects.
- The test projects use only `xunit.v3` and `Microsoft.AspNetCore.Mvc.Testing`. There is no assertion or mocking library, only the xUnit `Assert` and hand-written fakes. Pass `TestContext.Current.CancellationToken` to async calls, as the xUnit analyzer asks.
- `UnitTests` cover the Application logic, for example `UserService` with a fake `ICurrentUser`.
- `IntegrationTests` run the whole app in memory with `WebApplicationFactory<Program>` in the `Testing` environment with a test connection string. They cover authentication, health and the mapping of exceptions to ProblemDetails. The `ErrorTestController` exists only in the test project, is added as an ApplicationPart and throws each mapped exception.
