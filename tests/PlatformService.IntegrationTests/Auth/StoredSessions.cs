using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlatformService.Domain.Sessions;
using PlatformService.Infrastructure.Persistence;

namespace PlatformService.IntegrationTests.Auth;

/// <summary>Reads the sessions of a user from the database, with all of their refresh tokens.</summary>
internal static class StoredSessions
{
    public static async Task<List<Session>> OfUserAsync(PlatformApiFactory factory, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.Sessions
            .AsNoTracking()
            .Include(session => session.RefreshTokens)
            .Where(session => session.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
