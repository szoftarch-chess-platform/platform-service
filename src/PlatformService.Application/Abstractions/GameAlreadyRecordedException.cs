namespace PlatformService.Application.Abstractions;

/// <summary>
/// A parallel delivery stored the same game first, so <see cref="IUnitOfWork"/> saved nothing and forgot the game
/// (PS-ADR-13).
/// </summary>
/// <remarks>It is not an error response. A use case that does not expect it lets it become a 500.</remarks>
public sealed class GameAlreadyRecordedException(string message, Exception innerException)
    : Exception(message, innerException);
