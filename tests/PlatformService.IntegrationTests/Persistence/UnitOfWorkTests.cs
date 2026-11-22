using Microsoft.Extensions.DependencyInjection;
using PlatformService.Application.Abstractions;
using PlatformService.Application.Common.Exceptions;
using static PlatformService.IntegrationTests.TestNames;

namespace PlatformService.IntegrationTests.Persistence;

/// <summary>
/// Two registrations at the same time, which both pass the check of Identity before either is saved. The unique
/// index refuses the second commit, which becomes the same conflict as the check of Identity (PS-ADR-7, PS-ADR-11).
/// </summary>
public sealed class UnitOfWorkTests(PlatformApiFactory factory) : IClassFixture<PlatformApiFactory>
{
    [Fact]
    public async Task SaveChangesAsync_WithAUserNameTakenInTheMeantime_ThrowsAccountTakenException()
    {
        var userName = NewUserName();
        await using var first = await CreateUncommittedAsync(userName, $"{NewUserName()}@example.com");
        await using var second = await CreateUncommittedAsync(userName.ToUpperInvariant(), $"{NewUserName()}@example.com");
        await CommitAsync(first);

        var exception = await Assert.ThrowsAsync<AccountTakenException>(() => CommitAsync(second));

        Assert.Equal(TakenAccountFields.UserName, exception.Fields);
    }

    [Fact]
    public async Task SaveChangesAsync_WithAnEmailAddressTakenInTheMeantime_ThrowsAccountTakenException()
    {
        var email = $"{NewUserName()}@example.com";
        await using var first = await CreateUncommittedAsync(NewUserName(), email);
        await using var second = await CreateUncommittedAsync(NewUserName(), email.ToUpperInvariant());
        await CommitAsync(first);

        var exception = await Assert.ThrowsAsync<AccountTakenException>(() => CommitAsync(second));

        Assert.Equal(TakenAccountFields.Email, exception.Fields);
    }

    /// <summary>Creates an account in a scope of its own, as one request does, and leaves it uncommitted.</summary>
    private async Task<AsyncServiceScope> CreateUncommittedAsync(string userName, string email)
    {
        var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUserAccounts>().CreateAsync(
            Guid.CreateVersion7(), userName, email, "correct horse battery", TestContext.Current.CancellationToken);
        return scope;
    }

    private static Task CommitAsync(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(TestContext.Current.CancellationToken);
}
