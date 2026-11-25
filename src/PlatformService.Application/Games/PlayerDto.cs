namespace PlatformService.Application.Games;

/// <summary>
/// A player of a game, which is <c>{ kind: "user", userId, name }</c> for a user and <c>{ kind: "bot", name }</c> for
/// the bot (ADR-8).
/// </summary>
/// <param name="UserId">The id of a user. The JSON leaves it out for the bot.</param>
public sealed record PlayerDto(string Kind, Guid? UserId, string Name);
