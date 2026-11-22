using PlatformService.Application.Abstractions;
using PlatformService.Domain.Sessions;

namespace PlatformService.Infrastructure.Persistence;

internal sealed class SessionRepository(PlatformDbContext dbContext) : ISessionRepository
{
    public void Add(Session session) => dbContext.Sessions.Add(session);
}
