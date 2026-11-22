using PlatformService.Domain.Sessions;

namespace PlatformService.Application.Abstractions;

/// <summary>The repository of the <see cref="Session"/> aggregate (PS-ADR-7, PS-ADR-12).</summary>
public interface ISessionRepository
{
    /// <summary>Adds a new session with its refresh tokens, which the next commit of <see cref="IUnitOfWork"/> saves.</summary>
    void Add(Session session);
}
