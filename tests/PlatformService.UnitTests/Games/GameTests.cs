using PlatformService.Domain.Games;

namespace PlatformService.UnitTests.Games;

public sealed class GameTests
{
    private static readonly Guid _alice = Guid.CreateVersion7();
    private static readonly Guid _bob = Guid.CreateVersion7();

    [Fact]
    public void Record_ConvertsTheTimesToUtc()
    {
        var game = Record(
            started: new DateTimeOffset(2026, 11, 20, 15, 1, 40, TimeSpan.FromHours(1)),
            ended: new DateTimeOffset(2026, 11, 20, 15, 3, 12, TimeSpan.FromHours(1)));

        Assert.Equal(new DateTimeOffset(2026, 11, 20, 14, 1, 40, TimeSpan.Zero), game.StartedAt);
        Assert.Equal(TimeSpan.Zero, game.StartedAt.Offset);
        Assert.Equal(TimeSpan.Zero, game.EndedAt.Offset);
    }

    [Fact]
    public void Record_CopiesTheMoves()
    {
        List<string> moves = ["e2e4", "e7e5"];

        var game = Record(moves: moves);
        moves.Add("g1f3");

        Assert.Equal(["e2e4", "e7e5"], game.Moves);
    }

    [Fact]
    public void IsPlayedBy_IsTrueForBothColors()
    {
        var game = Record();

        Assert.True(game.IsPlayedBy(_alice));
        Assert.True(game.IsPlayedBy(_bob));
    }

    [Fact]
    public void IsPlayedBy_IsFalseForAnotherUser()
    {
        Assert.False(Record().IsPlayedBy(Guid.CreateVersion7()));
    }

    [Fact]
    public void IsPlayedBy_AgainstTheBot_IsTrueOnlyForTheUser()
    {
        var game = Record(white: Player.Create(Player.BotKind, null, "Stockfish bot"));

        Assert.True(game.IsPlayedBy(_bob));
        Assert.False(game.IsPlayedBy(_alice));
    }

    private static Game Record(
        Player? white = null,
        IEnumerable<string>? moves = null,
        DateTimeOffset? started = null,
        DateTimeOffset? ended = null) =>
        Game.Record(
            id: Guid.CreateVersion7(),
            variant: "standard",
            white: white ?? Player.Create(Player.UserKind, _alice, "alice"),
            black: Player.Create(Player.UserKind, _bob, "bob"),
            result: "whiteWon",
            reason: "checkmate",
            moves: moves ?? ["e2e4"],
            timeControl: new TimeControl(600000),
            startedAt: started ?? DateTimeOffset.UtcNow.AddMinutes(-5),
            endedAt: ended ?? DateTimeOffset.UtcNow);
}
