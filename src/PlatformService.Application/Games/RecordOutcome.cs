namespace PlatformService.Application.Games;

/// <summary>The outcome of <see cref="IFinishedGameService.RecordAsync"/>.</summary>
public enum RecordOutcome
{
    /// <summary>The game is stored now.</summary>
    Recorded,

    /// <summary>The game or its event was stored before, so nothing changed.</summary>
    AlreadyRecorded,
}
