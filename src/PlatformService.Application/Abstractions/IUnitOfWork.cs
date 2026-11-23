using PlatformService.Application.Common.Exceptions;

namespace PlatformService.Application.Abstractions;

/// <summary>
/// Saves every change of a use case in one commit at its end, so the changes succeed or fail together (PS-ADR-7).
/// </summary>
public interface IUnitOfWork
{
    /// <exception cref="AccountTakenException">
    /// A user name or an email address was taken in the meantime, for example by a parallel registration.
    /// </exception>
    /// <exception cref="ConcurrencyConflictException">
    /// A parallel request replaced the same refresh token first, so nothing was saved (PS-ADR-12).
    /// </exception>
    /// <exception cref="GameAlreadyRecordedException">
    /// A parallel delivery stored a game with the same id or from the same event first, so nothing was saved (PS-ADR-13).
    /// </exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
