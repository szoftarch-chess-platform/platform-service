using Microsoft.Extensions.Logging;
using PlatformService.Application.Abstractions;
using PlatformService.Domain.Sessions;

namespace PlatformService.Application.Auth;

internal sealed partial class SessionService(
    ISessionRepository sessions,
    IUserAccounts userAccounts,
    IAccessTokenIssuer accessTokens,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<SessionService> logger) : ISessionService
{
    public async Task<RefreshResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return RefreshResult.SessionEnded;
        }

        var tokenHash = RefreshTokens.Hash(refreshToken);
        try
        {
            return await TryRefreshAsync(tokenHash, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // A parallel refresh with the same token has just replaced it, so the second try is in the grace window.
            // That try only adds a token, so no other refresh can conflict with it (PS-ADR-12).
            return await TryRefreshAsync(tokenHash, cancellationToken);
        }
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        // A replaced or expired token still names its session, so the logout ends it all the same.
        var session = await sessions.FindByRefreshTokenAsync(RefreshTokens.Hash(refreshToken), cancellationToken);
        if (session is null)
        {
            return;
        }

        session.Revoke(timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<RefreshResult> TryRefreshAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        var session = await sessions.FindByRefreshTokenAsync(tokenHash, cancellationToken);
        if (session is null)
        {
            return RefreshResult.SessionEnded;
        }

        var now = timeProvider.GetUtcNow();
        var newRefreshToken = RefreshTokens.Create();
        switch (session.Refresh(tokenHash, newRefreshToken.Hash, now))
        {
            case RefreshOutcome.Rotated or RefreshOutcome.ReissuedInGraceWindow:
                break;

            case RefreshOutcome.ReuseDetected:
                await unitOfWork.SaveChangesAsync(cancellationToken);
                LogReuseDetected(logger, session.Id, session.UserId);
                return RefreshResult.SessionEnded;

            default:
                return RefreshResult.SessionEnded;
        }

        // The sessions are deleted with their user, so this misses only when the user is deleted right now.
        var account = await userAccounts.FindByIdAsync(session.UserId, cancellationToken);
        if (account is null)
        {
            return RefreshResult.SessionEnded;
        }

        var accessToken = accessTokens.Issue(account, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return RefreshResult.Refreshed(accessToken, new IssuedToken(newRefreshToken.Value, RefreshToken.Lifetime));
    }

    /// <summary>A replaced token that comes back usually means a stolen token (RFC 9700). The token itself is never logged.</summary>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "A refresh token that was replaced before the grace window came back, so session {SessionId} of user {UserId} is revoked.")]
    private static partial void LogReuseDetected(ILogger logger, Guid sessionId, Guid userId);
}
