namespace PlatformService.Application.Games;

/// <summary>One page of the finished games of the user, as returned by <c>GET /api/games</c>.</summary>
/// <param name="TotalCount">The number of all games of the user.</param>
public sealed record GamePageDto(IReadOnlyList<GameSummaryDto> Items, int Page, int PageSize, int TotalCount);
