using Microsoft.EntityFrameworkCore;

namespace PlatformService.Infrastructure.Persistence;

/// <summary>
/// The service's database and its Unit of Work. No entities yet.
/// </summary>
internal sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    // Picks up every IEntityTypeConfiguration<T> in this assembly.
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
}
