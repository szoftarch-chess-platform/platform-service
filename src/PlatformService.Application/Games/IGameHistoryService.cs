using FluentValidation;
using PlatformService.Application.Common.Exceptions;

namespace PlatformService.Application.Games;

/// <summary>The finished games of the signed-in user (F-6, PS-ADR-13).</summary>
public interface IGameHistoryService
{
    /// <summary>One page of the games of the user, with the newest first.</summary>
    /// <exception cref="ValidationException">The page or the page size is out of its range.</exception>
    Task<GamePageDto> ListAsync(ListGamesRequest request, CancellationToken cancellationToken);

    /// <summary>One game of the user with its moves.</summary>
    /// <exception cref="NotFoundException">There is no such game, or the user did not play in it.</exception>
    Task<GameDetailsDto> GetAsync(Guid gameId, CancellationToken cancellationToken);
}
