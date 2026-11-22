using PlatformService.Application.Common.Exceptions;

namespace PlatformService.Application.Abstractions;

/// <summary>
/// The accounts of ASP.NET Core Identity, whose user type stays in the Infrastructure (PS-ADR-11).
/// </summary>
public interface IUserAccounts
{
    /// <summary>Creates an account, which the next commit of <see cref="IUnitOfWork"/> saves.</summary>
    /// <exception cref="AccountTakenException">The user name or the email address is taken.</exception>
    Task<UserAccount> CreateAsync(Guid id, string userName, string email, string password, CancellationToken cancellationToken);

    /// <summary>Finds the account of the user name regardless of case, if the password is right.</summary>
    /// <returns>The account, or <see langword="null"/> if the user name is unknown or the password is wrong.</returns>
    Task<UserAccount?> FindByCredentialsAsync(string userName, string password, CancellationToken cancellationToken);
}
