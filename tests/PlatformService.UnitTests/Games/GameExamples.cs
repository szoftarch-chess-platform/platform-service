using System.Globalization;
using PlatformService.Application.Games;

namespace PlatformService.UnitTests.Games;

/// <summary>The two examples of <c>game.finished.v1</c> in <c>game-events.asyncapi.yaml</c> as commands.</summary>
internal static class GameExamples
{
    public static readonly Guid Alice = Guid.Parse("0193c2b4-7d3e-7c2a-9f1e-3b5a6c7d8e9f");
    public static readonly Guid Bob = Guid.Parse("0193c2b5-1f2e-7d3c-8b4a-5c6d7e8f9a0b");

    /// <summary>A user won against another user with a checkmate.</summary>
    public static RecordFinishedGameCommand WhiteWonByCheckmate() => new(
        EventId: Guid.Parse("0193c2c0-1a2b-7c3d-8e4f-5a6b7c8d9e0f"),
        GameId: Guid.Parse("0193c2bf-0a1b-7c2d-9e3f-4a5b6c7d8e9f"),
        Variant: "standard",
        White: new PlayerDto("user", Alice, "alice"),
        Black: new PlayerDto("user", Bob, "bob"),
        Result: "whiteWon",
        Reason: "checkmate",
        Moves: ["e2e4", "e7e5", "d1h5", "b8c6", "f1c4", "g8f6", "h5f7"],
        TimeControl: new TimeControlDto(600000),
        StartedAt: Time("2026-11-20T14:01:40.120Z"),
        EndedAt: Time("2026-11-20T14:03:12.345Z"));

    /// <summary>A user resigned against the bot.</summary>
    public static RecordFinishedGameCommand BotGameResigned() => new(
        EventId: Guid.Parse("0193c2d1-2b3c-7d4e-9f5a-6b7c8d9e0f1a"),
        GameId: Guid.Parse("0193c2cf-3c4d-7e5f-8a6b-7c8d9e0f1a2b"),
        Variant: "standard",
        White: new PlayerDto("bot", null, "Stockfish bot"),
        Black: new PlayerDto("user", Alice, "alice"),
        Result: "whiteWon",
        Reason: "resignation",
        Moves: ["e2e4", "c7c5"],
        TimeControl: new TimeControlDto(600000),
        StartedAt: Time("2026-11-20T15:09:30.000Z"),
        EndedAt: Time("2026-11-20T15:10:05.000Z"));

    public static DateTimeOffset Time(string rfc3339) => DateTimeOffset.Parse(rfc3339, CultureInfo.InvariantCulture);
}
