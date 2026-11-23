using FluentValidation;

namespace PlatformService.Application.Games;

/// <summary>Records the finished games that the game-service reports (ADR-6, PS-ADR-13).</summary>
public interface IFinishedGameService
{
    /// <summary>
    /// Stores the game for the history of its players, unless the game or its event is stored already, so that a
    /// repeated delivery changes nothing.
    /// </summary>
    /// <exception cref="ValidationException">The game breaks the schema of the contract.</exception>
    Task<RecordOutcome> RecordAsync(RecordFinishedGameCommand command, CancellationToken cancellationToken);
}
