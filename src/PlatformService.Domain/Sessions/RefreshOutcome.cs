namespace PlatformService.Domain.Sessions;

/// <summary>What <see cref="Session.Refresh"/> did with a refresh token, by the rules of ADR-4.</summary>
public enum RefreshOutcome
{
    /// <summary>The token had not been replaced yet, so it is replaced by the new one.</summary>
    Rotated,

    /// <summary>The token was replaced within the grace window, so the new one joins the token that replaced it.</summary>
    ReissuedInGraceWindow,

    /// <summary>The session was revoked earlier, so it issues no token.</summary>
    SessionRevoked,

    /// <summary>The token has expired.</summary>
    TokenExpired,

    /// <summary>The token was replaced before the grace window, so the session is revoked now.</summary>
    ReuseDetected,
}
