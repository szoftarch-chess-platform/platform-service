using PlatformService.Domain.Games;

namespace PlatformService.Application.Abstractions;

/// <summary>The repository of the <see cref="Game"/> aggregate (PS-ADR-7, PS-ADR-13).</summary>
public interface IGameRepository
{
    /// <summary>Adds a new game with the id of the event that reported it, which the next commit of <see cref="IUnitOfWork"/> saves.</summary>
    void Add(Game game, Guid eventId);

    /// <summary>Whether a game with this id or a game from this event is stored already.</summary>
    Task<bool> IsRecordedAsync(Guid gameId, Guid eventId, CancellationToken cancellationToken);

    /// <returns>The game, or <see langword="null"/> if no game has this id.</returns>
    Task<Game?> FindAsync(Guid gameId, CancellationToken cancellationToken);

    /// <summary>The number of the games in which the user played.</summary>
    Task<int> CountByPlayerAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The games in which the user played, ordered by their end and then by their id, both with the newest first,
    /// without the first <paramref name="skip"/> games.
    /// </summary>
    Task<IReadOnlyList<Game>> ListByPlayerAsync(Guid userId, int skip, int take, CancellationToken cancellationToken);
}
