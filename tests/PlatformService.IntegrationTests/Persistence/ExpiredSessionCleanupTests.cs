using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlatformService.Domain.Sessions;
using PlatformService.Infrastructure;
using PlatformService.Infrastructure.Persistence;
using PlatformService.IntegrationTests.Auth;
using static PlatformService.IntegrationTests.TestNames;

namespace PlatformService.IntegrationTests.Persistence;

/// <summary>
/// The cleanup of PS-ADR-12 at the real time. Only this test writes rows that have expired by then, because the
/// clocks of the other tests are never behind the real time, so it deletes no row of another test.
/// </summary>
public sealed class ExpiredSessionCleanupTests(PlatformApiFactory factory) : IClassFixture<PlatformApiFactory>
{
    [Fact]
    public async Task DeleteExpiredAsync_DeletesTheExpiredTokensAndTheSessionsWithoutAValidToken()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = await RegisterAsync();
        var current = Assert.Single(await StoredSessions.OfUserAsync(factory, userId));

        // The only token of this session expired a day ago.
        var expired = Session.Start(userId, NewHash(), now.AddDays(-8));

        // This session was refreshed 6 days ago, so its first token expired a day ago, and the second one is valid.
        var (first, second) = (NewHash(), NewHash());
        var refreshed = Session.Start(userId, first, now.AddDays(-8));
        refreshed.Refresh(first, second, now.AddDays(-6));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.Sessions.AddRange(expired, refreshed);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            await ExpiredSessionCleanup.DeleteExpiredAsync(db, now, TestContext.Current.CancellationToken);
        }

        var sessions = await StoredSessions.OfUserAsync(factory, userId);
        Assert.Equal(new[] { current.Id, refreshed.Id }.Order(), sessions.Select(session => session.Id).Order());
        Assert.Single(sessions.Single(session => session.Id == current.Id).RefreshTokens);
        Assert.Equal(second, Assert.Single(sessions.Single(session => session.Id == refreshed.Id).RefreshTokens).TokenHash);
    }

    [Fact]
    public async Task AddInfrastructure_RunsTheCleanupAsAHostedService()
    {
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection().AddInfrastructure(configuration);

        Assert.Contains(services, service =>
            service.ServiceType == typeof(IHostedService) && service.ImplementationType == typeof(ExpiredSessionCleanup));
    }

    private async Task<Guid> RegisterAsync()
    {
        using var client = AuthClient.Create(factory);
        using var response = await AuthClient.RegisterAsync(client, NewUserName());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadJsonAsync()).GetRequiredGuid("id");
    }

    private static byte[] NewHash() => RandomNumberGenerator.GetBytes(32);
}
