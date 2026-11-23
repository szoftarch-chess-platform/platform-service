namespace PlatformService.Infrastructure.Messaging;

/// <summary>The finished game in the <c>data</c> of the envelope.</summary>
internal sealed class FinishedGameData
{
    public Guid GameId { get; init; }

    public string? Variant { get; init; }

    public PlayerData? White { get; init; }

    public PlayerData? Black { get; init; }

    public string? Result { get; init; }

    public string? Reason { get; init; }

    public IReadOnlyList<string>? Moves { get; init; }

    public TimeControlData? TimeControl { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset EndedAt { get; init; }
}
