# PS-ADR-7: Persistence with EF Core

**Related:** NF-1, NF-3, ADR-4, ADR-5, [PS-ADR-1](clean-architecture.md), [PS-ADR-2](http-api.md)

## Decision

- The service accesses its database through EF Core with the Npgsql provider, so every query is parameterized.
- `Infrastructure/Persistence/PlatformDbContext.cs` has no `DbSet` yet. `OnModelCreating` collects the entity configurations with `ApplyConfigurationsFromAssembly`. Put one `IEntityTypeConfiguration<T>` per entity under `Persistence/`.
- `AddInfrastructure` sets up `UseNpgsql` with `ConnectionStrings:Platform` and `UseSnakeCaseNamingConvention`, and it adds `AddDbContextCheck<PlatformDbContext>` with the `ready` tag. The Api knows only the tag, and the DbContext is internal.
- `ConnectionStrings:Platform` is read through options with `ValidateOnStart`, so configuration that `WebApplicationFactory` adds later still applies. If the database is down, the app still starts.
- Each aggregate gets one repository interface in the Application, implemented in the Infrastructure. The `PlatformDbContext` is the Unit of Work. There is no generic repository. Repositories have no code yet, because there is no entity.

## Open questions

- The entities, the first migration and the migration strategy, which is either `MigrateAsync` at startup or a migration bundle. A bundle would run as a separate one-shot container in the compose of the infra repo.
- Where the UUID v7 is generated, in the application or with the PostgreSQL function `uuidv7()`. The byte order of `Guid.CreateVersion7` in .NET is debated, so with the first entity check that the ids are stored in time order in a `uuid` column.
