namespace PlatformService.Application.Games;

/// <summary>A finished game with its moves, as returned by <c>GET /api/games/{gameId}</c>.</summary>
/// <param name="Moves">Every move of the game in UCI notation and in order.</param>
public sealed record GameDetailsDto(
    Guid Id,
    string Variant,
    PlayerDto White,
    PlayerDto Black,
    string Result,
    string Reason,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    TimeControlDto TimeControl,
    IReadOnlyList<string> Moves);
