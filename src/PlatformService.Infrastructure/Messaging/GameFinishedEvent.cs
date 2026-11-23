using PlatformService.Application.Games;

namespace PlatformService.Infrastructure.Messaging;

/// <summary>
/// The envelope of <c>game.finished.v1</c> in <c>game-events.asyncapi.yaml</c> as the consumer reads it (ADR-6). The
/// fields that the service does not need, such as <c>source</c> and <c>time</c>, are ignored.
/// </summary>
/// <remarks>
/// Any field may be missing from a message, so every field accepts a missing value, and the validator of the command
/// finds what is missing (PS-ADR-13).
/// </remarks>
internal sealed class GameFinishedEvent
{
    public const string TypeName = "game.finished.v1";

    public Guid Id { get; init; }

    public string? Type { get; init; }

    public FinishedGameData? Data { get; init; }

    /// <summary>The game of the event with the id of the event. A missing value stays missing for the validator.</summary>
    public RecordFinishedGameCommand ToCommand()
    {
        var data = Data ?? new FinishedGameData();
        return new RecordFinishedGameCommand(
            Id,
            data.GameId,
            data.Variant!,
            ToPlayer(data.White),
            ToPlayer(data.Black),
            data.Result!,
            data.Reason!,
            data.Moves!,
            data.TimeControl is null ? null! : new TimeControlDto(data.TimeControl.InitialMs),
            data.StartedAt,
            data.EndedAt);
    }

    private static PlayerDto ToPlayer(PlayerData? player) =>
        player is null ? null! : new PlayerDto(player.Kind!, player.UserId, player.Name!);
}
