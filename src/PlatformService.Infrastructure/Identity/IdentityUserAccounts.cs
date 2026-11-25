using Microsoft.AspNetCore.Identity;
using PlatformService.Application.Abstractions;
using PlatformService.Application.Common.Exceptions;

namespace PlatformService.Infrastructure.Identity;

/// <summary>The accounts of the Application on <see cref="UserManager{TUser}"/> of Identity (PS-ADR-11).</summary>
/// <remarks><see cref="UserManager{TUser}"/> takes no cancellation token, so the tokens of the port go unused.</remarks>
internal sealed class IdentityUserAccounts(UserManager<PlatformUser> userManager) : IUserAccounts
{
    public async Task<UserAccount> CreateAsync(
        Guid id,
        string userName,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var user = new PlatformUser { Id = id, UserName = userName, Email = email };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw ToException(result);
        }

        return new UserAccount(user.Id, userName);
    }

    public async Task<UserAccount?> FindByCredentialsAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByNameAsync(userName);
        if (user is null || !await userManager.CheckPasswordAsync(user, password))
        {
            return null;
        }

        return new UserAccount(user.Id, user.UserName!);
    }

    public async Task<UserAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        return user is null ? null : new UserAccount(user.Id, user.UserName!);
    }

    /// <summary>
    /// The validators of PS-ADR-6 check every other rule first, so Identity refuses an account only because of a
    /// taken user name or email address. Any other error means that the two sets of rules differ.
    /// </summary>
    private static Exception ToException(IdentityResult result)
    {
        var codes = result.Errors.Select(error => error.Code).ToHashSet(StringComparer.Ordinal);

        var taken = TakenAccountFields.None;
        if (codes.Remove(nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            taken |= TakenAccountFields.UserName;
        }

        if (codes.Remove(nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            taken |= TakenAccountFields.Email;
        }

        if (codes.Count > 0 || taken == TakenAccountFields.None)
        {
            return new InvalidOperationException(
                $"Identity refused an account that the validators accepted: {string.Join(", ", result.Errors.Select(error => error.Code))}.");
        }

        return new AccountTakenException(taken);
    }
}
