using Microsoft.AspNetCore.Mvc;
using PlatformService.Application.Games;

namespace PlatformService.Api.Controllers;

/// <summary>The finished games of the signed-in user (PS-ADR-2, PS-ADR-13).</summary>
[ApiController]
[Route("api/games")]
public sealed class GamesController(IGameHistoryService gameHistory) : ControllerBase
{
    /// <summary>One page of the finished games in which the user played, with the newest first.</summary>
    [HttpGet]
    [ProducesResponseType<GamePageDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<GamePageDto>> List(
        [FromQuery] int page = ListGamesRequest.DefaultPage,
        [FromQuery] int pageSize = ListGamesRequest.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        await gameHistory.ListAsync(new ListGamesRequest(page, pageSize), cancellationToken);

    /// <summary>A finished game of the user with its moves.</summary>
    /// <remarks>The route has no constraint, so an id that is not a UUID gets 400 and not 404, as in the contract.</remarks>
    [HttpGet("{gameId}")]
    [ProducesResponseType<GameDetailsDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<GameDetailsDto>> Get(Guid gameId, CancellationToken cancellationToken) =>
        await gameHistory.GetAsync(gameId, cancellationToken);
}
