namespace PlatformService.Application.Games;

/// <summary>The time of each user for the whole game in milliseconds (ADR-8).</summary>
public sealed record TimeControlDto(long InitialMs);
