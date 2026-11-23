using FluentValidation;
using PlatformService.Application.Games;
using PlatformService.UnitTests.Fakes;

namespace PlatformService.UnitTests.Games;

public sealed class FinishedGameServiceTests
{
    // What the fakes did, in order.
    private readonly List<string> _log = [];
    private readonly FakeGameRepository _games;
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly FinishedGameService _service;

    public FinishedGameServiceTests()
    {
        _games = new FakeGameRepository(_log);
        _unitOfWork = new FakeUnitOfWork(_log);
        _service = new FinishedGameService(new RecordFinishedGameCommandValidator(), _games, _unitOfWork);
    }

    [Fact]
    public async Task RecordAsync_StoresTheGameWithItsEventInOneCommit()
    {
        var command = GameExamples.WhiteWonByCheckmate();

        var outcome = await _service.RecordAsync(command, TestContext.Current.CancellationToken);

        Assert.Equal(RecordOutcome.Recorded, outcome);
        Assert.Equal(["add game", "commit"], _log);
        var (game, eventId) = Assert.Single(_games.Added);
        Assert.Equal(command.EventId, eventId);
        Assert.Equal(command.GameId, game.Id);
        Assert.Equal("standard", game.Variant);
        Assert.Equal(("user", GameExamples.Alice, "alice"), (game.White.Kind, game.White.UserId, game.White.Name));
        Assert.Equal(("user", GameExamples.Bob, "bob"), (game.Black.Kind, game.Black.UserId, game.Black.Name));
        Assert.Equal(("whiteWon", "checkmate"), (game.Result, game.Reason));
        Assert.Equal(command.Moves, game.Moves);
        Assert.Equal(600000, game.TimeControl.InitialMs);
        Assert.Equal(command.StartedAt, game.StartedAt);
        Assert.Equal(command.EndedAt, game.EndedAt);
    }

    [Fact]
    public async Task RecordAsync_AgainstTheBot_StoresTheBotWithoutAUserId()
    {
        await _service.RecordAsync(GameExamples.BotGameResigned(), TestContext.Current.CancellationToken);

        var (game, _) = Assert.Single(_games.Added);
        Assert.Equal(("bot", null, "Stockfish bot"), (game.White.Kind, game.White.UserId, game.White.Name));
    }

    [Fact]
    public async Task RecordAsync_WithTheSameEventAgain_ChangesNothing()
    {
        await _service.RecordAsync(GameExamples.WhiteWonByCheckmate(), TestContext.Current.CancellationToken);
        _log.Clear();

        var outcome = await _service.RecordAsync(GameExamples.WhiteWonByCheckmate(), TestContext.Current.CancellationToken);

        Assert.Equal(RecordOutcome.AlreadyRecorded, outcome);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task RecordAsync_WithTheSameGameInAnotherEvent_ChangesNothing()
    {
        await _service.RecordAsync(GameExamples.WhiteWonByCheckmate(), TestContext.Current.CancellationToken);
        _log.Clear();

        var outcome = await _service.RecordAsync(
            GameExamples.WhiteWonByCheckmate() with { EventId = Guid.CreateVersion7() },
            TestContext.Current.CancellationToken);

        Assert.Equal(RecordOutcome.AlreadyRecorded, outcome);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task RecordAsync_WhenAParallelDeliveryCommitsFirst_ReportsTheGameAsRecorded()
    {
        _unitOfWork.RecordedGameOnNextCommit = true;

        var outcome = await _service.RecordAsync(GameExamples.WhiteWonByCheckmate(), TestContext.Current.CancellationToken);

        Assert.Equal(RecordOutcome.AlreadyRecorded, outcome);
        Assert.Equal(["add game", "recorded game"], _log);
    }

    [Fact]
    public async Task RecordAsync_WithAnInvalidGame_ThrowsBeforeItChangesAnything()
    {
        var command = GameExamples.WhiteWonByCheckmate() with { Moves = ["e2-e4"] };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.RecordAsync(command, TestContext.Current.CancellationToken));

        Assert.Empty(_log);
    }
}
