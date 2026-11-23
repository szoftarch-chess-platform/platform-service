namespace PlatformService.Infrastructure.Messaging;

/// <summary>A player of the game. The bot has no user id (ADR-8).</summary>
internal sealed class PlayerData
{
    public string? Kind { get; init; }

    public Guid? UserId { get; init; }

    public string? Name { get; init; }
}
