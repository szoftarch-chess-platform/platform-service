# PS-ADR-7: Persistence with EF Core

**Related:** NF-1, NF-3, ADR-4, ADR-5, [PS-ADR-1](clean-architecture.md), [PS-ADR-2](http-api.md), [PS-ADR-8](configuration-and-logging.md), [PS-ADR-10](testing.md), [PS-ADR-11](accounts.md), [PS-ADR-12](tokens-and-sessions.md), [PS-ADR-13](game-history.md)

## Decision

- The service accesses its database through EF Core with the Npgsql provider, so every query is parameterized.
- `Infrastructure/Persistence/PlatformDbContext.cs` collects the entity configurations in `OnModelCreating` with `ApplyConfigurationsFromAssembly`. Put one `IEntityTypeConfiguration<T>` per entity under `Persistence/`.
- `AddInfrastructure` sets up `UseNpgsql` with `ConnectionStrings:Platform` and `UseSnakeCaseNamingConvention`, and it adds `AddDbContextCheck<PlatformDbContext>` with the `ready` tag. The Api knows only the tag, and the DbContext is internal.
- The convention renames only the names that EF Core derives. A name that a library sets explicitly, such as the tables and the indexes of Identity, gets its snake case name in the entity configuration.
- `ConnectionStrings:Platform` is read through options with `ValidateOnStart`, so configuration that `WebApplicationFactory` adds later still applies. If the database is down, the app still starts.
- Each aggregate gets one repository interface in the Application, implemented in the Infrastructure. There is no generic repository. The sessions have `ISessionRepository` (PS-ADR-12), the games have `IGameRepository` (PS-ADR-13), and the accounts use the stores of Identity through the `IUserAccounts` port (PS-ADR-11).
- **Unit of Work:** the `PlatformDbContext` is the Unit of Work behind the `IUnitOfWork` port of the Application. A use case saves all of its changes with one call of `SaveChangesAsync` at its end. A violated unique index of the user name or the email address becomes the conflict of PS-ADR-11. A failed condition of a concurrency token becomes `ConcurrencyConflictException`, and the Unit of Work forgets the loaded entities, so that the use case can load them again and retry, as the refresh of PS-ADR-12 does. A violated primary key or unique index of the event of a game becomes `GameAlreadyRecordedException`, and the Unit of Work forgets the game (PS-ADR-13).
- **Ids:** the service creates a UUID v7 with `Guid.CreateVersion7` and the time of the `TimeProvider` of PS-ADR-8.
- **Migrations:** the migrations are in `Infrastructure/Persistence/Migrations`. The EF Core tools run as the `dotnet-ef` local tool of `dotnet-tools.json`, and a design-time factory in the Infrastructure creates the DbContext for them without starting the service. It reads the connection string from `ConnectionStrings__Platform`.
- **Migration bundle:** the `Dockerfile` builds the bundle into the image as `/app/efbundle`. The compose of the infra repo runs it in the one-shot `platform-migrations` container of the same image before the service starts, as ADR-5 decides. The service does not migrate at startup.

## Rationale

- Npgsql writes a `Guid` into a `uuid` column in the byte order of RFC 9562, so the ids stay in the order of their time, which a test of PS-ADR-10 checks. The debated byte order of .NET applies only to `Guid.ToByteArray`.
- An id from the application is known before the insert, and the `TimeProvider` lets the tests choose its time.
- One commit at the end of a use case saves its changes together or not at all, without an explicit transaction.
- With the design-time factory the tools and the bundle need neither the Api nor its settings, and the Design package stays out of the image (PS-ADR-1).

## Rejected alternatives

- **`uuidv7()` of PostgreSQL as the default value:** the id would be known only after the insert, and the tests could not set its time.
- **The Api as the startup project of the tools:** the Design package would have to be in the Api, so its compilers would get into the image, and the bundle would start the composition root of the service.
