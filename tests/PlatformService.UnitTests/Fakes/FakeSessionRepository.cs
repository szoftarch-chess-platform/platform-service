using PlatformService.Application.Abstractions;
using PlatformService.Domain.Sessions;

namespace PlatformService.UnitTests.Fakes;

/// <summary>Sessions in memory, which keep every change of a use case even when its commit fails.</summary>
internal sealed class FakeSessionRepository(List<string> log) : ISessionRepository
{
    public List<Session> Added { get; } = [];

    public void Add(Session session)
    {
        log.Add("add session");
        Added.Add(session);
    }

    public Task<Session?> FindByRefreshTokenAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Added.Find(session =>
            session.RefreshTokens.Any(token => token.TokenHash.AsSpan().SequenceEqual(tokenHash))));
}
