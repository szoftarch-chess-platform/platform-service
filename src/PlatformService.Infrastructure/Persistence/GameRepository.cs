using Microsoft.EntityFrameworkCore;
using PlatformService.Application.Abstractions;
using PlatformService.Domain.Games;

namespace PlatformService.Infrastructure.Persistence;

internal sealed class GameRepository(PlatformDbContext dbContext) : IGameRepository
{
    public void Add(Game game, Guid eventId) =>
        dbContext.Games.Add(game).Property<Guid>(GameConfiguration.EventIdProperty).CurrentValue = eventId;

    public Task<bool> IsRecordedAsync(Guid gameId, Guid eventId, CancellationToken cancellationToken) =>
        dbContext.Games.AnyAsync(
            game => game.Id == gameId || EF.Property<Guid>(game, GameConfiguration.EventIdProperty) == eventId,
            cancellationToken);

    public Task<Game?> FindAsync(Guid gameId, CancellationToken cancellationToken) =>
        dbContext.Games.AsNoTracking().SingleOrDefaultAsync(game => game.Id == gameId, cancellationToken);

    public Task<int> CountByPlayerAsync(Guid userId, CancellationToken cancellationToken) =>
        PlayedBy(userId).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<Game>> ListByPlayerAsync(
        Guid userId, int skip, int take, CancellationToken cancellationToken) =>
        await PlayedBy(userId)
            .OrderByDescending(game => game.EndedAt)
            .ThenByDescending(game => game.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    /// <summary>PostgreSQL combines the indexes of the two user id columns for this condition (PS-ADR-13).</summary>
    private IQueryable<Game> PlayedBy(Guid userId) =>
        dbContext.Games.AsNoTracking()
            .Where(game => game.White.UserId == userId || game.Black.UserId == userId);
}
