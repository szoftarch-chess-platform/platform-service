namespace PlatformService.Domain.Games;

/// <summary>The time control of a <see cref="Game"/>, which is the time of each user for the whole game (ADR-8).</summary>
public sealed record TimeControl(long InitialMs);
