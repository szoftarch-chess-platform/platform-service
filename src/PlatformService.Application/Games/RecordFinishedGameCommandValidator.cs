using System.Text.RegularExpressions;
using FluentValidation;
using PlatformService.Domain.Games;

namespace PlatformService.Application.Games;

/// <summary>
/// The rules of the schema of <c>game.finished.v1</c>. The variant, the result, the reason and the kind of a player
/// may have any value, so a value that a later version of the contract adds passes (ADR-7).
/// </summary>
/// <remarks>The command comes from a message, so every rule accepts a missing value and fails on it.</remarks>
internal sealed partial class RecordFinishedGameCommandValidator : AbstractValidator<RecordFinishedGameCommand>
{
    public const string MoveNotUci = "Every move must be in UCI notation, such as e2e4 or e7e8q.";

    public RecordFinishedGameCommandValidator()
    {
        RuleFor(command => command.EventId).NotEmpty();
        RuleFor(command => command.GameId).NotEmpty();
        RuleFor(command => command.Variant).NotEmpty();
        RuleFor(command => command.Result).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty();

        RuleFor(command => command.White).NotNull().SetValidator(new PlayerValidator());
        RuleFor(command => command.Black).NotNull().SetValidator(new PlayerValidator());

        RuleFor(command => command.Moves).NotNull();
        RuleForEach(command => command.Moves)
            .Must(move => move is not null && UciMove().IsMatch(move))
            .WithMessage(MoveNotUci);

        RuleFor(command => command.TimeControl).NotNull().SetValidator(new TimeControlValidator());

        RuleFor(command => command.StartedAt).NotEmpty();
        RuleFor(command => command.EndedAt).NotEmpty();
    }

    // \z instead of $, because in .NET $ also matches before a final line break (PS-ADR-6).
    [GeneratedRegex(@"^[a-h][1-8][a-h][1-8][qrbn]?\z")]
    private static partial Regex UciMove();

    /// <summary>Every player has a kind and a name, and a user has an id too.</summary>
    private sealed class PlayerValidator : AbstractValidator<PlayerDto>
    {
        public PlayerValidator()
        {
            RuleFor(player => player.Kind).NotEmpty();
            RuleFor(player => player.Name).NotEmpty();
            RuleFor(player => player.UserId)
                .NotEmpty()
                .When(player => player.Kind == Player.UserKind);
        }
    }

    /// <summary>The time of each user is at least one millisecond, as the minimum of the contract says.</summary>
    private sealed class TimeControlValidator : AbstractValidator<TimeControlDto>
    {
        public TimeControlValidator()
        {
            RuleFor(timeControl => timeControl.InitialMs).GreaterThanOrEqualTo(1);
        }
    }
}
