# PS-ADR-10: Tests with xUnit v3, hand-written fakes and Testcontainers

**Related:** ADR-6, [PS-ADR-1](clean-architecture.md), [PS-ADR-5](error-handling.md), [PS-ADR-7](persistence.md), [PS-ADR-11](accounts.md), [PS-ADR-12](tokens-and-sessions.md), [PS-ADR-13](game-history.md), [PS-ADR-14](messaging.md)

## Context

The Application logic and the HTTP behavior of the service need automated tests. The accounts and the data of the service rely on PostgreSQL itself, such as its unique indexes and its migrations, and the consumer of the finished games relies on RabbitMQ itself, such as its permissions and its delivery limit, which a fake cannot show.

## Decision

- The tests use xUnit v3. `dotnet test` runs them on Microsoft Testing Platform, which the `test.runner` setting of `global.json` selects.
- The test projects use only `xunit.v3`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql` and `Testcontainers.RabbitMq`. There is no assertion or mocking library, only the xUnit `Assert` and hand-written fakes. Pass `TestContext.Current.CancellationToken` to async calls, as the xUnit analyzer asks.
- `UnitTests` cover the Domain and the Application logic, for example `AuthService` with fakes of its ports. Time-dependent logic gets `ManualTimeProvider`, a hand-written clock that moves only when the test moves it. They need no Docker.
- `IntegrationTests` run the whole app in memory with `WebApplicationFactory<Program>` in the `Testing` environment. They cover authentication, health, the JSON conventions, the mapping of exceptions to ProblemDetails, the storage in PostgreSQL with its concurrency tokens, the auth endpoints with their tokens and cookies, the cleanup of the expired sessions, the game history of PS-ADR-13 with its endpoints and the indexes of its migration, and the consumer of PS-ADR-14 with its answers to the broker. Every test of the history records its games through the use case with new user ids, so it sees only its own games in the shared database. The test project adds its own controllers as an ApplicationPart. The `ErrorTestController` throws each mapped exception, and the `JsonTestController` answers with an enum and a property without a value.
- An assembly fixture starts one PostgreSQL container for the integration tests with Testcontainers, from the image of the infra compose, and applies the migrations of PS-ADR-7. The tests share the database, so each one creates its own data with new names.
- Another assembly fixture starts one RabbitMQ container from the image of the infra compose. Every test of the consumer creates a virtual host of its own with the user of the service, the permissions of ADR-6 and the policy of the infra repo with delays of milliseconds. The test plays the part of the game-service there with the admin user.
- The hosts of the integration tests run without the consumer, because its queue belongs to the virtual host of one test. `WithConsumer` of the factory adds it with a retry delay of 100 milliseconds, and it can put a layer that fails in front of the use case.
- The service does not start without its signing key (PS-ADR-12), so an assembly fixture writes a new P-256 key to a temporary file for the integration tests.
- The app of the integration tests takes its time from `TestClock`, a hand-written clock that follows the real time and that a test can move ahead, for example past the grace window of PS-ADR-12. It is never behind the real time, so no test writes rows that are older than the real time.
- The hosts of the integration tests run without the hosted cleanup of PS-ADR-12, because it would delete the rows of other tests that are expired by the clock of its own host. Its test calls the cleanup directly at the real time.

## Rationale

- A real PostgreSQL shows what the service relies on, such as the unique indexes of PS-ADR-11, the order of the UUID v7 ids of PS-ADR-7 and a model without a missing migration. The in-memory provider of EF Core and SQLite behave differently.
- One container for the assembly keeps the tests fast, and new names per test let them run in parallel without cleaning up.
- A real RabbitMQ shows what the consumer relies on, such as the permissions of ADR-6, the policy of the infra repo and the different counting of `basic.nack` and `basic.reject`. A virtual host for each test keeps the names of the queues of PS-ADR-14, like the new names in the shared database.

## Consequences

- Docker has to run for the integration tests, locally and in the CI.
- The policy of the tests is a copy of the policy of the infra repo, so a change of the policy is made in both places.
