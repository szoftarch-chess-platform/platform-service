using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using PlatformService.Infrastructure.Persistence;

namespace PlatformService.Infrastructure.Identity;

/// <summary>
/// The EF Core store of Identity without its own save. The commit of the use case saves the user together with the
/// other changes, such as the session of a registration (PS-ADR-11).
/// </summary>
internal sealed class PlatformUserStore : UserOnlyStore<PlatformUser, PlatformDbContext, Guid>
{
    public PlatformUserStore(PlatformDbContext context, IdentityErrorDescriber? describer = null)
        : base(context, describer)
    {
        AutoSaveChanges = false;
    }
}
