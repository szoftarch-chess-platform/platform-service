namespace PlatformService.Domain.Games;

/// <summary>A player of a <see cref="Game"/>, which is a user or the bot (ADR-8).</summary>
/// <remarks>
/// The kind is a string, so a kind that a later version of the contract adds passes through unchanged (ADR-7).
/// </remarks>
public sealed record Player
{
    public const string UserKind = "user";
    public const string BotKind = "bot";

    private Player(string kind, Guid? userId, string name)
    {
        Kind = kind;
        UserId = userId;
        Name = name;
    }

    public string Kind { get; }

    /// <summary>The id of the user, or <see langword="null"/> if the player is not a user.</summary>
    public Guid? UserId { get; }

    /// <summary>The user name of a user, or the name of the bot.</summary>
    public string Name { get; }

    /// <summary>
    /// Creates a player. Only a player of the kind <see cref="UserKind"/> keeps the user id, so the bot or an unknown
    /// kind never puts the game into the history of a user.
    /// </summary>
    public static Player Create(string kind, Guid? userId, string name) =>
        new(kind, kind == UserKind ? userId : null, name);
}
