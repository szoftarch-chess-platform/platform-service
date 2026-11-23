namespace PlatformService.Application.Games;

/// <summary>A finished game on a page of <c>GET /api/games</c>, without its moves.</summary>
public sealed record GameSummaryDto(
    Guid Id,
    string Variant,
    PlayerDto White,
    PlayerDto Black,
    string Result,
    string Reason,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt);
