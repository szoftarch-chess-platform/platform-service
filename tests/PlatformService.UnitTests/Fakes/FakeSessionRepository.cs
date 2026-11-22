using PlatformService.Application.Abstractions;
using PlatformService.Domain.Sessions;

namespace PlatformService.UnitTests.Fakes;

internal sealed class FakeSessionRepository(List<string> log) : ISessionRepository
{
    public List<Session> Added { get; } = [];

    public void Add(Session session)
    {
        log.Add("add session");
        Added.Add(session);
    }
}
