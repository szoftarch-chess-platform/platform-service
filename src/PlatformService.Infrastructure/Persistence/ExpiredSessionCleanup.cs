using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlatformService.Domain.Sessions;

namespace PlatformService.Infrastructure.Persistence;

/// <summary>
/// Deletes the expired refresh tokens and the sessions without a valid token, at startup and then every hour
/// (PS-ADR-12). The rules refuse an expired token anyway, so the cleanup only keeps the tables small.
/// </summary>
/// <remarks>The statements are idempotent, so several instances of the service can run them without a lock.</remarks>
internal sealed partial class ExpiredSessionCleanup(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ExpiredSessionCleanup> logger) : BackgroundService
{
    public static TimeSpan Interval { get; } = TimeSpan.FromHours(1);

    /// <summary>Deletes what has expired at <paramref name="now"/>, and returns the number of deleted rows.</summary>
    public static async Task<(int Sessions, int RefreshTokens)> DeleteExpiredAsync(
        PlatformDbContext dbContext,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // The database deletes the tokens of these sessions with them.
        var sessions = await dbContext.Sessions
            .Where(session => !session.RefreshTokens.Any(token => token.ExpiresAt > now))
            .ExecuteDeleteAsync(cancellationToken);

        // The expired tokens of the sessions that still have a valid one.
        var refreshTokens = await dbContext.Set<RefreshToken>()
            .Where(token => token.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);

        return (sessions, refreshTokens);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var (sessions, refreshTokens) = await DeleteExpiredAsync(dbContext, timeProvider.GetUtcNow(), stoppingToken);
            if (sessions > 0 || refreshTokens > 0)
            {
                LogDeleted(logger, sessions, refreshTokens);
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            // An unhandled exception would stop the whole service, for example while the database is down.
            LogFailed(logger, exception, Interval);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {SessionCount} expired sessions and {RefreshTokenCount} expired refresh tokens.")]
    private static partial void LogDeleted(ILogger logger, int sessionCount, int refreshTokenCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "The cleanup of the expired sessions failed, and it runs again in {Interval}.")]
    private static partial void LogFailed(ILogger logger, Exception exception, TimeSpan interval);
}
