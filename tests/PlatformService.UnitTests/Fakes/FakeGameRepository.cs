using PlatformService.Application.Abstractions;
using PlatformService.Domain.Games;

namespace PlatformService.UnitTests.Fakes;

/// <summary>Games in memory, which keep every change of a use case even when its commit fails.</summary>
internal sealed class FakeGameRepository(List<string> log) : IGameRepository
{
    public List<(Game Game, Guid EventId)> Added { get; } = [];

    public void Add(Game game, Guid eventId)
    {
        log.Add("add game");
        Added.Add((game, eventId));
    }

    public Task<bool> IsRecordedAsync(Guid gameId, Guid eventId, CancellationToken cancellationToken) =>
        Task.FromResult(Added.Exists(added => added.Game.Id == gameId || added.EventId == eventId));

    public Task<Game?> FindAsync(Guid gameId, CancellationToken cancellationToken) =>
        Task.FromResult(Added.Select(added => added.Game).FirstOrDefault(game => game.Id == gameId));

    public Task<int> CountByPlayerAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(PlayedBy(userId).Count());

    public Task<IReadOnlyList<Game>> ListByPlayerAsync(Guid userId, int skip, int take, CancellationToken cancellationToken)
    {
        log.Add($"list {skip} {take}");
        IReadOnlyList<Game> games = [.. PlayedBy(userId)
            .OrderByDescending(game => game.EndedAt)
            // PostgreSQL orders the ids like their lowercase strings.
            .ThenByDescending(game => game.Id.ToString(), StringComparer.Ordinal)
            .Skip(skip)
            .Take(take)];
        return Task.FromResult(games);
    }

    private IEnumerable<Game> PlayedBy(Guid userId) =>
        Added.Select(added => added.Game).Where(game => game.IsPlayedBy(userId));
}
