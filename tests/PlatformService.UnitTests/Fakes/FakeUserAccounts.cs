using PlatformService.Application.Abstractions;
using PlatformService.Application.Common.Exceptions;

namespace PlatformService.UnitTests.Fakes;

/// <summary>Accounts in memory, with user names and email addresses that are unique regardless of case.</summary>
internal sealed class FakeUserAccounts(List<string> log) : IUserAccounts
{
    private readonly List<(UserAccount Account, string Email, string Password)> _accounts = [];

    public Task<UserAccount> CreateAsync(Guid id, string userName, string email, string password, CancellationToken cancellationToken)
    {
        var taken = TakenAccountFields.None;
        if (_accounts.Exists(entry => SameText(entry.Account.UserName, userName)))
        {
            taken |= TakenAccountFields.UserName;
        }

        if (_accounts.Exists(entry => SameText(entry.Email, email)))
        {
            taken |= TakenAccountFields.Email;
        }

        if (taken != TakenAccountFields.None)
        {
            throw new AccountTakenException(taken);
        }

        log.Add($"create {userName}");
        var account = new UserAccount(id, userName);
        _accounts.Add((account, email, password));
        return Task.FromResult(account);
    }

    public Task<UserAccount?> FindByCredentialsAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var entry = _accounts.Find(entry => SameText(entry.Account.UserName, userName) && entry.Password == password);
        return Task.FromResult<UserAccount?>(entry.Account);
    }

    /// <summary>Adds an account directly, as if an earlier registration had saved it.</summary>
    public UserAccount Seed(string userName, string email, string password)
    {
        var account = new UserAccount(Guid.CreateVersion7(), userName);
        _accounts.Add((account, email, password));
        return account;
    }

    private static bool SameText(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
