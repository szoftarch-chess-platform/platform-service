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
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
