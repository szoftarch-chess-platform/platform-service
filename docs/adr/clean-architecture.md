# PS-ADR-1: Clean Architecture in four projects

**Related:** ADR-1, [PS-ADR-7](persistence.md), [PS-ADR-10](testing.md), [PS-ADR-11](accounts.md), [PS-ADR-12](tokens-and-sessions.md)

## Context

The business logic has to stay independent of the web framework and of the database access.

## Decision

- Clean Architecture in four projects. The project references enforce the dependency rule.
  - **Domain** references nothing and has no NuGet packages.
  - **Application** references Domain and uses `FluentValidation` and `FluentValidation.DependencyInjectionExtensions`, and `Microsoft.Extensions.Logging.Abstractions` for the security events of PS-ADR-12. It must not depend on ASP.NET Core or EF Core.
  - **Infrastructure** references Application and uses `Npgsql.EntityFrameworkCore.PostgreSQL`, `EFCore.NamingConventions`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` and `Microsoft.AspNetCore.Identity.EntityFrameworkCore` for the accounts of PS-ADR-11 and `Microsoft.IdentityModel.JsonWebTokens` for the access tokens of PS-ADR-12. It also uses `Microsoft.EntityFrameworkCore.Design` with `PrivateAssets="all"` for the EF Core tools of PS-ADR-7, so the package and its compilers do not reach the Api and its image.
  - **Api** references Application and Infrastructure and uses `Microsoft.AspNetCore.OpenApi` and `Scalar.AspNetCore`.
- The Application defines the ports, and the outer layers implement them.
- Each layer has one DI extension method, which are `AddApplication`, `AddInfrastructure` and `AddPresentation`. The last two take the configuration. `Program.cs` only calls these and sets up the middleware.
- Each feature has one use case interface and one implementation, for example `IUserService` and `UserService` in `Application/Users/`. There is no mediator library.
- Mapping is written by hand, without AutoMapper.
- Implementations are `internal sealed`. Interfaces, DTOs and exceptions are public. `InternalsVisibleTo` opens the Application to the unit tests and the Infrastructure to the integration tests.
- Package versions are the latest stable ones that support net10.0, pinned in `Directory.Packages.props` with Central Package Management. The 11.0.x Microsoft packages target net11.0 only, so they stay on 10.0.x.
