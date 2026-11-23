namespace PlatformService.Application.Games;

/// <summary>
/// A finished game of the event <c>game.finished.v1</c> in <c>game-events.asyncapi.yaml</c>, with the id of the event
/// that reported it (ADR-6, PS-ADR-13).
/// </summary>
/// <param name="Moves">Every move of the game in UCI notation and in order.</param>
public sealed record RecordFinishedGameCommand(
    Guid EventId,
    Guid GameId,
    string Variant,
    PlayerDto White,
    PlayerDto Black,
    string Result,
    string Reason,
    IReadOnlyList<string> Moves,
    TimeControlDto TimeControl,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt);
