namespace PlatformService.Application.Auth;

/// <summary>Use cases that renew the tokens of a session and end it (ADR-4, PS-ADR-12).</summary>
public interface ISessionService
{
    /// <summary>
    /// Replaces the refresh token with a new one and issues a new access token, with the grace window and the reuse
    /// detection of ADR-4.
    /// </summary>
    /// <param name="refreshToken">The token from the cookie, which can be missing.</param>
    Task<RefreshResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken);

    /// <summary>Revokes the session of the refresh token. Without a known token it changes nothing.</summary>
    /// <param name="refreshToken">The token from the cookie, which can be missing.</param>
    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken);
}
