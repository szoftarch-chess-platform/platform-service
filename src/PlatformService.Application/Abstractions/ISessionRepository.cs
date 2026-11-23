using PlatformService.Domain.Sessions;

namespace PlatformService.Application.Abstractions;

/// <summary>The repository of the <see cref="Session"/> aggregate (PS-ADR-7, PS-ADR-12).</summary>
public interface ISessionRepository
{
    /// <summary>Adds a new session with its refresh tokens, which the next commit of <see cref="IUnitOfWork"/> saves.</summary>
    void Add(Session session);

    /// <summary>
    /// Finds the session of a refresh token by the hash of the token, whatever the state of the token. The session
    /// comes with this one token only, because the rules of a refresh need no other.
    /// </summary>
    /// <returns>The session, or <see langword="null"/> if no session has a token with this hash.</returns>
    Task<Session?> FindByRefreshTokenAsync(byte[] tokenHash, CancellationToken cancellationToken);
}
