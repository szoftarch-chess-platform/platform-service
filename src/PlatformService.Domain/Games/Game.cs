namespace PlatformService.Domain.Games;

/// <summary>A finished game in the history of its players, as the game-service reported it (ADR-6, PS-ADR-13).</summary>
/// <remarks>
/// The variant, the result and the reason are strings, so a value that a later version of the contract adds passes
/// through unchanged (ADR-7).
/// </remarks>
public sealed class Game
{
    private Game(
        Guid id,
        string variant,
        Player white,
        Player black,
        string result,
        string reason,
        IReadOnlyList<string> moves,
        TimeControl timeControl,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt)
        : this(
            id: id,
            variant: variant,
            result: result,
            reason: reason,
            moves: moves,
            startedAt: startedAt,
            endedAt: endedAt)
    {
        White = white;
        Black = black;
        TimeControl = timeControl;
    }

    // EF Core cannot bind complex types to the parameters of a constructor, so it creates the game with this one and
    // sets the players and the time control afterwards.
#pragma warning disable CS8618
    private Game(
        Guid id,
        string variant,
        string result,
        string reason,
        IReadOnlyList<string> moves,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt)
#pragma warning restore CS8618
    {
        Id = id;
        Variant = variant;
        Result = result;
        Reason = reason;
        Moves = moves;
        StartedAt = startedAt;
        EndedAt = endedAt;
    }

    /// <summary>The id of the game, which the game-service creates as a UUID v7 (ADR-8).</summary>
    public Guid Id { get; }

    public string Variant { get; }

    public Player White { get; }

    public Player Black { get; }

    public string Result { get; }

    public string Reason { get; }

    /// <summary>Every move of the game in UCI notation and in order. It is empty if the game ended before the first move.</summary>
    public IReadOnlyList<string> Moves { get; }

    public TimeControl TimeControl { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset EndedAt { get; }

    /// <summary>Records a finished game with its times converted to UTC (PS-ADR-8).</summary>
    /// <remarks>
    /// Neighboring parameters share a type, such as the two players, the result and the reason or the two times, so
    /// the callers name every argument.
    /// </remarks>
    public static Game Record(
        Guid id,
        string variant,
        Player white,
        Player black,
        string result,
        string reason,
        IEnumerable<string> moves,
        TimeControl timeControl,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt) =>
        new(
            id: id,
            variant: variant,
            white: white,
            black: black,
            result: result,
            reason: reason,
            moves: [.. moves],
            timeControl: timeControl,
            startedAt: startedAt.ToUniversalTime(),
            endedAt: endedAt.ToUniversalTime());

    /// <summary>Whether the user played in the game with either color.</summary>
    public bool IsPlayedBy(Guid userId) => White.UserId == userId || Black.UserId == userId;
}
