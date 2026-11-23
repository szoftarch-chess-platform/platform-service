using FluentValidation;
using PlatformService.Application.Abstractions;
using PlatformService.Domain.Games;

namespace PlatformService.Application.Games;

internal sealed class FinishedGameService(
    IValidator<RecordFinishedGameCommand> validator,
    IGameRepository games,
    IUnitOfWork unitOfWork) : IFinishedGameService
{
    public async Task<RecordOutcome> RecordAsync(RecordFinishedGameCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        // A repeated delivery usually comes after the commit, so it is found here without a failed insert.
        if (await games.IsRecordedAsync(command.GameId, command.EventId, cancellationToken))
        {
            return RecordOutcome.AlreadyRecorded;
        }

        var game = Game.Record(
            id: command.GameId,
            variant: command.Variant,
            white: ToPlayer(command.White),
            black: ToPlayer(command.Black),
            result: command.Result,
            reason: command.Reason,
            moves: command.Moves,
            timeControl: new TimeControl(command.TimeControl.InitialMs),
            startedAt: command.StartedAt,
            endedAt: command.EndedAt);
        games.Add(game, command.EventId);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (GameAlreadyRecordedException)
        {
            // A parallel delivery of the same game committed between the check and the commit.
            return RecordOutcome.AlreadyRecorded;
        }

        return RecordOutcome.Recorded;
    }

    private static Player ToPlayer(PlayerDto player) => Player.Create(player.Kind, player.UserId, player.Name);
}
