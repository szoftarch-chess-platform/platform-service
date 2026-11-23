using Microsoft.EntityFrameworkCore;
using PlatformService.Application.Abstractions;
using PlatformService.Domain.Sessions;

namespace PlatformService.Infrastructure.Persistence;

internal sealed class SessionRepository(PlatformDbContext dbContext) : ISessionRepository
{
    public void Add(Session session) => dbContext.Sessions.Add(session);

    public Task<Session?> FindByRefreshTokenAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        dbContext.Sessions
            .Include(session => session.RefreshTokens.Where(token => token.TokenHash == tokenHash))
            .Where(session => session.RefreshTokens.Any(token => token.TokenHash == tokenHash))
            .SingleOrDefaultAsync(cancellationToken);
}
