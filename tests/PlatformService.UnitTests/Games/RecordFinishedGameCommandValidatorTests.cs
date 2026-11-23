using PlatformService.Application.Games;

namespace PlatformService.UnitTests.Games;

public sealed class RecordFinishedGameCommandValidatorTests
{
    private readonly RecordFinishedGameCommandValidator _validator = new();

    [Fact]
    public void TheExamplesOfTheContract_AreValid()
    {
        Assert.True(_validator.Validate(GameExamples.WhiteWonByCheckmate()).IsValid);
        Assert.True(_validator.Validate(GameExamples.BotGameResigned()).IsValid);
    }

    [Fact]
    public void AGameWithoutMoves_IsValid()
    {
        Assert.True(_validator.Validate(GameExamples.WhiteWonByCheckmate() with { Moves = [] }).IsValid);
    }

    // A reader accepts the values that a later version of the contract adds (ADR-7).
    [Fact]
    public void UnknownValuesOfTheEnums_AreValid()
    {
        var command = GameExamples.WhiteWonByCheckmate() with
        {
            Variant = "chess960",
            Result = "adjudicated",
            Reason = "agreement",
            Black = new PlayerDto("guest", null, "visitor"),
        };

        Assert.True(_validator.Validate(command).IsValid);
    }

    [Theory]
    [InlineData("e7e8q")]
    [InlineData("e1g1")]
    [InlineData("a7a8n")]
    public void Move_InUciNotation_IsValid(string move)
    {
        Assert.True(_validator.Validate(GameExamples.WhiteWonByCheckmate() with { Moves = [move] }).IsValid);
    }

    [Theory]
    [InlineData("e2e4\n")]
    [InlineData("E2E4")]
    [InlineData("e2-e4")]
    [InlineData("e7e8Q")]
    [InlineData("e7e8k")]
    [InlineData("i2i4")]
    [InlineData("e9e4")]
    [InlineData("Nf3")]
    [InlineData("")]
    [InlineData(null)]
    public void Move_NotInUciNotation_HasItsMessage(string? move)
    {
        var result = _validator.Validate(GameExamples.WhiteWonByCheckmate() with { Moves = [move!] });

        var error = Assert.Single(result.Errors);
        Assert.Equal("Moves[0]", error.PropertyName);
        Assert.Equal(RecordFinishedGameCommandValidator.MoveNotUci, error.ErrorMessage);
    }

    [Fact]
    public void AUserWithoutAnId_IsInvalid()
    {
        var result = _validator.Validate(GameExamples.WhiteWonByCheckmate() with { White = new PlayerDto("user", null, "alice") });

        Assert.Equal("White.UserId", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void ABotWithoutAnId_IsValid()
    {
        Assert.True(_validator.Validate(GameExamples.BotGameResigned()).IsValid);
    }

    [Fact]
    public void MissingValues_AreInvalid()
    {
        var command = new RecordFinishedGameCommand(
            Guid.Empty, Guid.Empty, null!, null!, null!, null!, null!, null!, null!, default, default);

        var properties = _validator.Validate(command).Errors.Select(error => error.PropertyName);

        string[] expected =
            ["EventId", "GameId", "Variant", "White", "Black", "Result", "Reason", "Moves", "TimeControl", "StartedAt", "EndedAt"];
        Assert.Equal(expected.Order(StringComparer.Ordinal), properties.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void APlayerWithoutKindOrName_IsInvalid()
    {
        var result = _validator.Validate(GameExamples.WhiteWonByCheckmate() with { Black = new PlayerDto("", null, "") });

        Assert.Equal(["Black.Kind", "Black.Name"], result.Errors.Select(error => error.PropertyName).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InitialTime_BelowOneMillisecond_IsInvalid(long initialMs)
    {
        var result = _validator.Validate(GameExamples.WhiteWonByCheckmate() with { TimeControl = new TimeControlDto(initialMs) });

        Assert.Equal("TimeControl.InitialMs", Assert.Single(result.Errors).PropertyName);
    }
}
