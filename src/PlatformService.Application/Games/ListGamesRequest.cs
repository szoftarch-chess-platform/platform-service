namespace PlatformService.Application.Games;

/// <summary>The query of <c>GET /api/games</c>.</summary>
/// <param name="Page">The number of the page, starting at 1.</param>
/// <param name="PageSize">The number of games on a page.</param>
public sealed record ListGamesRequest(int Page, int PageSize)
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
