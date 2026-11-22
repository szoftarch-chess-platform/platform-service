using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PlatformService.Domain.Sessions;
using PlatformService.Infrastructure.Identity;

namespace PlatformService.Infrastructure.Persistence;

/// <summary>
/// The service's database and its Unit of Work. It holds the users of Identity without roles and their sessions.
/// </summary>
internal sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
    : IdentityUserContext<PlatformUser, Guid>(options)
{
    public DbSet<Session> Sessions => Set<Session>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity builds its own model first, and the configurations of this assembly complete it.
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
    }
}
