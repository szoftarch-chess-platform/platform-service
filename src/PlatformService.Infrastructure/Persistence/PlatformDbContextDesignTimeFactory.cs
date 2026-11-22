using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PlatformService.Infrastructure.Persistence;

/// <summary>
/// Creates the DbContext for the EF Core tools and for the migration bundle without starting the service.
/// </summary>
/// <remarks>
/// The connection string comes from the same <c>ConnectionStrings__Platform</c> environment variable as in the
/// service. Adding a migration needs none, and the bundle also takes one with its <c>--connection</c> option.
/// </remarks>
internal sealed class PlatformDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            $"ConnectionStrings__{DatabaseOptions.ConnectionStringName}");

        var builder = new DbContextOptionsBuilder<PlatformDbContext>();
        builder.UsePlatformDatabase(connectionString);
        return new PlatformDbContext(builder.Options);
    }
}
