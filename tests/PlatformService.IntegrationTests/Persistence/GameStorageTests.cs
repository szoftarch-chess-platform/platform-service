using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlatformService.Application.Abstractions;
using PlatformService.Application.Games;
using PlatformService.Domain.Games;
using PlatformService.Infrastructure.Persistence;
using static PlatformService.IntegrationTests.Games.RecordedGames;

namespace PlatformService.IntegrationTests.Persistence;

/// <summary>
/// The games in PostgreSQL. Two deliveries of the same game both pass the check of the use case, and the primary key
/// or the unique index of the event refuses the second commit (PS-ADR-7, PS-ADR-13).
/// </summary>
public sealed class GameStorageTests(PlatformApiFactory factory) : IClassFixture<PlatformApiFactory>
{
    private static readonly DateTimeOffset _end = new(2026, 11, 20, 14, 3, 12, 345, TimeSpan.Zero);

    [Fact]
    public async Task RecordAsync_StoresEveryPartOfTheGame()
    {
        var command = NewGame(User(Guid.CreateVersion7()), Bot, _end);

        Assert.Equal(RecordOutcome.Recorded, await RecordAsync(factory, command));

        var game = await FindAsync(command.GameId);
        Assert.Equal(("standard", "whiteWon", "checkmate"), (game.Variant, game.Result, game.Reason));
        Assert.Equal(Player.Create("user", command.White.UserId, "alice"), game.White);
        Assert.Equal(Player.Create("bot", null, "Stockfish bot"), game.Black);
        Assert.Equal(command.Moves, game.Moves);
        Assert.Equal(new TimeControl(600000), game.TimeControl);
        Assert.Equal(command.StartedAt, game.StartedAt);
        Assert.Equal(command.EndedAt, game.EndedAt);
        Assert.Equal(command.EventId, await StoredEventIdAsync(command.GameId));
    }

    [Fact]
    public async Task RecordAsync_WithoutMoves_StoresAnEmptyArray()
    {
        var command = NewGame(User(Guid.CreateVersion7()), Bot, _end) with { Result = "aborted", Reason = "engineFailure", Moves = [] };

        await RecordAsync(factory, command);

        Assert.Empty((await FindAsync(command.GameId)).Moves);
    }

    // The reader accepts any offset, and the database keeps the same moment in UTC (ADR-8).
    [Fact]
    public async Task RecordAsync_WithAnOffset_StoresTheSameMomentInUtc()
    {
        var endedAt = new DateTimeOffset(2026, 11, 20, 15, 3, 12, 345, TimeSpan.FromHours(1));
        var command = NewGame(User(Guid.CreateVersion7()), Bot, endedAt);

        await RecordAsync(factory, command);

        var game = await FindAsync(command.GameId);
        Assert.Equal(endedAt, game.EndedAt);
        Assert.Equal(TimeSpan.Zero, game.EndedAt.Offset);
    }

    [Fact]
    public async Task RecordAsync_WithTheSameGameOrEventAgain_StoresTheGameOnce()
    {
        var command = NewGame(User(Guid.CreateVersion7()), Bot, _end);
        await RecordAsync(factory, command);

        Assert.Equal(RecordOutcome.AlreadyRecorded, await RecordAsync(factory, command));
        Assert.Equal(RecordOutcome.AlreadyRecorded, await RecordAsync(factory, command with { EventId = Guid.CreateVersion7() }));
        Assert.Equal(RecordOutcome.AlreadyRecorded, await RecordAsync(factory, command with { GameId = Guid.CreateVersion7() }));
        Assert.Equal(1, await CountAsync(command.White.UserId!.Value));
    }

    [Fact]
    public async Task SaveChangesAsync_WithTheSameGameFromAParallelDelivery_ThrowsGameAlreadyRecordedException()
    {
        var command = NewGame(User(Guid.CreateVersion7()), Bot, _end);
        await using var first = AddUncommitted(command);
        await using var second = AddUncommitted(command with { EventId = Guid.CreateVersion7() });
        await CommitAsync(first);

        await Assert.ThrowsAsync<GameAlreadyRecordedException>(() => CommitAsync(second));
    }

    [Fact]
    public async Task SaveChangesAsync_WithTheSameEventFromAParallelDelivery_ThrowsGameAlreadyRecordedException()
    {
        var command = NewGame(User(Guid.CreateVersion7()), Bot, _end);
        await using var first = AddUncommitted(command);
        await using var second = AddUncommitted(command with { GameId = Guid.CreateVersion7() });
        await CommitAsync(first);

        await Assert.ThrowsAsync<GameAlreadyRecordedException>(() => CommitAsync(second));
    }

    // The model does not know these indexes, because the migration creates them with SQL (PS-ADR-13).
    [Fact]
    public async Task Migrations_CreateTheIndexesOfTheTwoUserIds()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var definitions = await db.Database
            .SqlQueryRaw<string>("SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'games'")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Contains("CREATE INDEX ix_games_white_user_id ON public.games USING btree (white_user_id)", definitions);
        Assert.Contains("CREATE INDEX ix_games_black_user_id ON public.games USING btree (black_user_id)", definitions);
    }

    private async Task<Game> FindAsync(Guid gameId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var game = await scope.ServiceProvider.GetRequiredService<IGameRepository>()
            .FindAsync(gameId, TestContext.Current.CancellationToken);
        Assert.NotNull(game);
        return game;
    }

    private async Task<int> CountAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IGameRepository>()
            .CountByPlayerAsync(userId, TestContext.Current.CancellationToken);
    }

    private async Task<Guid> StoredEventIdAsync(Guid gameId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Games
            .Where(game => game.Id == gameId)
            .Select(game => EF.Property<Guid>(game, GameConfiguration.EventIdProperty))
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Adds the game in a scope of its own, as one delivery does, and leaves it uncommitted.</summary>
    private AsyncServiceScope AddUncommitted(RecordFinishedGameCommand command)
    {
        var scope = factory.Services.CreateAsyncScope();
        var game = Game.Record(
            id: command.GameId,
            variant: command.Variant,
            white: Player.Create(command.White.Kind, command.White.UserId, command.White.Name),
            black: Player.Create(command.Black.Kind, command.Black.UserId, command.Black.Name),
            result: command.Result,
            reason: command.Reason,
            moves: command.Moves,
            timeControl: new TimeControl(command.TimeControl.InitialMs),
            startedAt: command.StartedAt,
            endedAt: command.EndedAt);
        scope.ServiceProvider.GetRequiredService<IGameRepository>().Add(game, command.EventId);
        return scope;
    }

    private static Task CommitAsync(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(TestContext.Current.CancellationToken);
}
