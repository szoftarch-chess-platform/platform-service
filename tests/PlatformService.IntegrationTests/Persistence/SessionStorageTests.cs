using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using PlatformService.Application.Abstractions;
using PlatformService.Domain.Sessions;
using PlatformService.IntegrationTests.Auth;
using static PlatformService.IntegrationTests.TestNames;

namespace PlatformService.IntegrationTests.Persistence;

/// <summary>
/// The sessions in PostgreSQL. Two refreshes with the same token load it before either commits, and the condition of
/// the concurrency token refuses the second commit (PS-ADR-7, PS-ADR-12).
/// </summary>
public sealed class SessionStorageTests(PlatformApiFactory factory) : IClassFixture<PlatformApiFactory>
{
    [Fact]
    public async Task FindByRefreshTokenAsync_LoadsTheSessionWithThatTokenOnly()
    {
        var refreshToken = await RegisterAsync();
        await using (var rotation = await RefreshUncommittedAsync(AuthClient.Hash(refreshToken)))
        {
            await CommitAsync(rotation);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var session = await FindAsync(scope, AuthClient.Hash(refreshToken));

        Assert.Equal(AuthClient.Hash(refreshToken), Assert.Single(session.RefreshTokens).TokenHash);
    }

    [Fact]
    public async Task SaveChangesAsync_WithATokenReplacedInTheMeantime_ThrowsConcurrencyConflictException()
    {
        var tokenHash = AuthClient.Hash(await RegisterAsync());
        await using var first = await RefreshUncommittedAsync(tokenHash);
        await using var second = await RefreshUncommittedAsync(tokenHash);
        await CommitAsync(first);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => CommitAsync(second));
    }

    [Fact]
    public async Task SaveChangesAsync_AfterAConflict_LoadsTheReplacedTokenAgainForTheGraceWindow()
    {
        var tokenHash = AuthClient.Hash(await RegisterAsync());
        await using var first = await RefreshUncommittedAsync(tokenHash);
        await using var second = await RefreshUncommittedAsync(tokenHash);
        await CommitAsync(first);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => CommitAsync(second));

        var session = await FindAsync(second, tokenHash);

        Assert.NotNull(Assert.Single(session.RefreshTokens).ReplacedAt);
        Assert.Equal(RefreshOutcome.ReissuedInGraceWindow, session.Refresh(tokenHash, NewHash(), DateTimeOffset.UtcNow));
        await CommitAsync(second);
    }

    private async Task<string> RegisterAsync()
    {
        using var client = AuthClient.Create(factory);
        using var response = await AuthClient.RegisterAsync(client, NewUserName());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return AuthClient.RefreshToken(response);
    }

    /// <summary>Rotates the token in a scope of its own, as one request does, and leaves it uncommitted.</summary>
    private async Task<AsyncServiceScope> RefreshUncommittedAsync(byte[] tokenHash)
    {
        var scope = factory.Services.CreateAsyncScope();
        var session = await FindAsync(scope, tokenHash);
        Assert.Equal(RefreshOutcome.Rotated, session.Refresh(tokenHash, NewHash(), DateTimeOffset.UtcNow));
        return scope;
    }

    private static async Task<Session> FindAsync(AsyncServiceScope scope, byte[] tokenHash)
    {
        var session = await scope.ServiceProvider.GetRequiredService<ISessionRepository>()
            .FindByRefreshTokenAsync(tokenHash, TestContext.Current.CancellationToken);
        Assert.NotNull(session);
        return session;
    }

    private static Task CommitAsync(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(TestContext.Current.CancellationToken);

    private static byte[] NewHash() => RandomNumberGenerator.GetBytes(32);
}
