using Microsoft.Extensions.DependencyInjection;
using PlatformService.Application.Games;

namespace PlatformService.IntegrationTests.Games;

/// <summary>
/// Finished games for the tests, shaped like the examples of <c>game-events.asyncapi.yaml</c>. Every game gets new ids,
/// because the tests share one database.
/// </summary>
internal static class RecordedGames
{
    public static readonly PlayerDto Bot = new("bot", null, "Stockfish bot");

    public static PlayerDto User(Guid userId, string name = "alice") => new("user", userId, name);

    public static RecordFinishedGameCommand NewGame(
        PlayerDto white,
        PlayerDto black,
        DateTimeOffset endedAt,
        Guid? gameId = null) => new(
        EventId: Guid.CreateVersion7(),
        GameId: gameId ?? Guid.CreateVersion7(),
        Variant: "standard",
        White: white,
        Black: black,
        Result: "whiteWon",
        Reason: "checkmate",
        Moves: ["e2e4", "e7e5", "d1h5", "b8c6", "f1c4", "g8f6", "h5f7"],
        TimeControl: new TimeControlDto(600000),
        StartedAt: endedAt.AddMinutes(-2),
        EndedAt: endedAt);

    /// <summary>Records the game through the use case, as the consumer of the events does.</summary>
    public static async Task<RecordOutcome> RecordAsync(PlatformApiFactory factory, RecordFinishedGameCommand command)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IFinishedGameService>()
            .RecordAsync(command, TestContext.Current.CancellationToken);
    }

    /// <summary>Two UUID v7 ids in the order in which PostgreSQL sorts them, which is the order of their strings.</summary>
    public static (Guid Lower, Guid Higher) TwoIds()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        Array.Sort(ids, (first, second) => string.CompareOrdinal(first.ToString(), second.ToString()));
        return (ids[0], ids[1]);
    }
}
