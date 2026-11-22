namespace PlatformService.Application.Abstractions;

/// <summary>
/// A parallel request changed the same data first, so <see cref="IUnitOfWork"/> saved nothing. The loaded data is
/// forgotten too, so the use case can load it again and try once more (PS-ADR-7, PS-ADR-12).
/// </summary>
/// <remarks>It is not an error response. A use case that does not expect it lets it become a 500.</remarks>
public sealed class ConcurrencyConflictException(string message, Exception innerException)
    : Exception(message, innerException);
