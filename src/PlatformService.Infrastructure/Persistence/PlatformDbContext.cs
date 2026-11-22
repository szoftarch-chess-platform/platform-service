using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PlatformService.Infrastructure.Identity;

namespace PlatformService.Infrastructure.Persistence;

/// <summary>
/// The service's database and its Unit of Work. It holds the users of Identity without roles.
/// </summary>
internal sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
    : IdentityUserContext<PlatformUser, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Identity builds its own model first, and the configurations of this assembly complete it.
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
    }
}
