using FluentValidation;
using PlatformService.Application.Common.Exceptions;
using PlatformService.Application.Games;
using PlatformService.Domain.Games;
using PlatformService.UnitTests.Fakes;

namespace PlatformService.UnitTests.Games;

public sealed class GameHistoryServiceTests
{
    private static readonly DateTimeOffset _end = new(2026, 11, 20, 14, 0, 0, TimeSpan.Zero);
    private static readonly Player _alice = Player.Create(Player.UserKind, GameExamples.Alice, "alice");
    private static readonly Player _bob = Player.Create(Player.UserKind, GameExamples.Bob, "bob");
    private static readonly Player _bot = Player.Create(Player.BotKind, null, "Stockfish bot");

    // What the fakes did, in order.
    private readonly List<string> _log = [];
    private readonly FakeGameRepository _games;
    private readonly GameHistoryService _service;

    public GameHistoryServiceTests()
    {
        _games = new FakeGameRepository(_log);
        _service = new GameHistoryService(new ListGamesRequestValidator(), _games, new FakeCurrentUser(GameExamples.Alice, "alice"));
    }

    [Fact]
    public async Task ListAsync_ReturnsThePageWithTheCountOfEveryGameOfTheUser()
    {
        var games = Enumerable.Range(0, 5).Select(minutes => AddGame(_end.AddMinutes(minutes))).ToList();
        AddGame(_end, white: OtherUser(), black: OtherUser());

        var page = await _service.ListAsync(new ListGamesRequest(2, 2), TestContext.Current.CancellationToken);

        Assert.Equal([games[2].Id, games[1].Id], page.Items.Select(game => game.Id));
        Assert.Equal((2, 2, 5), (page.Page, page.PageSize, page.TotalCount));
        Assert.Equal(["list 2 2"], _log);
    }

    [Fact]
    public async Task ListAsync_MapsTheSummaryOfAGame()
    {
        var game = AddGame(_end, white: _bot, black: _alice);

        var page = await _service.ListAsync(new ListGamesRequest(1, 20), TestContext.Current.CancellationToken);

        var summary = Assert.Single(page.Items);
        Assert.Equal(game.Id, summary.Id);
        Assert.Equal("standard", summary.Variant);
        Assert.Equal(new PlayerDto("bot", null, "Stockfish bot"), summary.White);
        Assert.Equal(new PlayerDto("user", GameExamples.Alice, "alice"), summary.Black);
        Assert.Equal(("whiteWon", "resignation"), (summary.Result, summary.Reason));
        Assert.Equal(game.StartedAt, summary.StartedAt);
        Assert.Equal(game.EndedAt, summary.EndedAt);
    }

    // The offset of the last page number does not fit into an int.
    [Theory]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    public async Task ListAsync_AfterTheLastPage_ReturnsNoGamesWithoutListingThem(int pageNumber)
    {
        AddGame(_end);
        AddGame(_end.AddMinutes(1));
        AddGame(_end.AddMinutes(2));

        var page = await _service.ListAsync(new ListGamesRequest(pageNumber, 1), TestContext.Current.CancellationToken);

        Assert.Empty(page.Items);
        Assert.Equal(3, page.TotalCount);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task ListAsync_WithAnInvalidPage_Throws()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.ListAsync(new ListGamesRequest(0, 20), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAsync_ReturnsTheGameWithItsMoves()
    {
        var game = AddGame(_end);

        var details = await _service.GetAsync(game.Id, TestContext.Current.CancellationToken);

        Assert.Equal(game.Id, details.Id);
        Assert.Equal(new PlayerDto("user", GameExamples.Alice, "alice"), details.White);
        Assert.Equal(new PlayerDto("user", GameExamples.Bob, "bob"), details.Black);
        Assert.Equal(new TimeControlDto(600000), details.TimeControl);
        Assert.Equal(["e2e4", "c7c5"], details.Moves);
    }

    [Fact]
    public async Task GetAsync_OfAGameThatDoesNotExist_ThrowsNotFound()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken));

        Assert.Equal(GameHistoryService.GameNotFound, exception.Message);
    }

    [Fact]
    public async Task GetAsync_OfAGameOfOtherUsers_ThrowsTheSameNotFound()
    {
        var game = AddGame(_end, white: OtherUser(), black: OtherUser());

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetAsync(game.Id, TestContext.Current.CancellationToken));

        Assert.Equal(GameHistoryService.GameNotFound, exception.Message);
    }

    /// <summary>Adds a game that alice played as white against bob unless the test names the players.</summary>
    private Game AddGame(DateTimeOffset endedAt, Player? white = null, Player? black = null)
    {
        var game = Game.Record(
            id: Guid.CreateVersion7(),
            variant: "standard",
            white: white ?? _alice,
            black: black ?? _bob,
            result: "whiteWon",
            reason: "resignation",
            moves: ["e2e4", "c7c5"],
            timeControl: new TimeControl(600000),
            startedAt: endedAt.AddMinutes(-1),
            endedAt: endedAt);
        _games.Added.Add((game, Guid.CreateVersion7()));
        return game;
    }

    private static Player OtherUser() => Player.Create(Player.UserKind, Guid.CreateVersion7(), "someone");
}
