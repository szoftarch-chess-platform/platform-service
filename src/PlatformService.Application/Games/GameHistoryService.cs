using FluentValidation;
using PlatformService.Application.Abstractions;
using PlatformService.Application.Common.Exceptions;
using PlatformService.Domain.Games;

namespace PlatformService.Application.Games;

internal sealed class GameHistoryService(
    IValidator<ListGamesRequest> validator,
    IGameRepository games,
    ICurrentUser currentUser) : IGameHistoryService
{
    /// <summary>
    /// The same for a missing game and for a game of other users, so the response tells nothing about the games of
    /// others.
    /// </summary>
    public const string GameNotFound = "The game does not exist.";

    public async Task<GamePageDto> ListAsync(ListGamesRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var userId = currentUser.UserId;
        var totalCount = await games.CountByPlayerAsync(userId, cancellationToken);

        // In long, because a large page number times the page size does not fit into an int.
        var skip = (long)(request.Page - 1) * request.PageSize;
        var items = skip < totalCount
            ? await games.ListByPlayerAsync(userId, (int)skip, request.PageSize, cancellationToken)
            : [];

        return new GamePageDto([.. items.Select(ToSummary)], request.Page, request.PageSize, totalCount);
    }

    public async Task<GameDetailsDto> GetAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var game = await games.FindAsync(gameId, cancellationToken);
        if (game is null || !game.IsPlayedBy(currentUser.UserId))
        {
            throw new NotFoundException(GameNotFound);
        }

        return new GameDetailsDto(
            Id: game.Id,
            Variant: game.Variant,
            White: ToDto(game.White),
            Black: ToDto(game.Black),
            Result: game.Result,
            Reason: game.Reason,
            StartedAt: game.StartedAt,
            EndedAt: game.EndedAt,
            TimeControl: new TimeControlDto(game.TimeControl.InitialMs),
            Moves: game.Moves);
    }

    private static GameSummaryDto ToSummary(Game game) => new(
        Id: game.Id,
        Variant: game.Variant,
        White: ToDto(game.White),
        Black: ToDto(game.Black),
        Result: game.Result,
        Reason: game.Reason,
        StartedAt: game.StartedAt,
        EndedAt: game.EndedAt);

    private static PlayerDto ToDto(Player player) => new(player.Kind, player.UserId, player.Name);
}
