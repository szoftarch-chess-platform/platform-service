namespace PlatformService.Domain.Sessions;

/// <summary>
/// The refresh tokens that come from one registration or sign-in of a user through refreshes (ADR-4, PS-ADR-12).
/// </summary>
public sealed class Session
{
    private readonly List<RefreshToken> _refreshTokens = [];

    private Session(Guid id, Guid userId, DateTimeOffset startedAt)
    {
        Id = id;
        UserId = userId;
        StartedAt = startedAt;
    }

    /// <summary>How long a replaced refresh token still gets a new one, so that parallel refreshes end no session (ADR-4).</summary>
    public static TimeSpan GraceWindow { get; } = TimeSpan.FromSeconds(10);

    public Guid Id { get; }

    public Guid UserId { get; }

    public DateTimeOffset StartedAt { get; }

    /// <summary>When a logout or a detected reuse revoked the session, or <see langword="null"/> while it is valid.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>The refresh tokens that were loaded with the session, which can be only the one of a refresh.</summary>
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens;

    /// <summary>Starts the session of a registration or a sign-in with its first refresh token.</summary>
    /// <param name="refreshTokenHash">The SHA-256 hash of the refresh token. The session never holds the token itself.</param>
    public static Session Start(Guid userId, byte[] refreshTokenHash, DateTimeOffset now)
    {
        var session = new Session(Guid.CreateVersion7(now), userId, now);
        session._refreshTokens.Add(RefreshToken.Issue(refreshTokenHash, now));
        return session;
    }

    /// <summary>
    /// Applies the rules of ADR-4 to a refresh with the token of <paramref name="presentedTokenHash"/>. If the outcome
    /// is <see cref="RefreshOutcome.Rotated"/> or <see cref="RefreshOutcome.ReissuedInGraceWindow"/>, the session has a
    /// new token with <paramref name="newTokenHash"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The session was loaded without the presented token.</exception>
    public RefreshOutcome Refresh(byte[] presentedTokenHash, byte[] newTokenHash, DateTimeOffset now)
    {
        var token = _refreshTokens.Find(candidate => candidate.TokenHash.AsSpan().SequenceEqual(presentedTokenHash))
            ?? throw new ArgumentException("The session has no refresh token with this hash.", nameof(presentedTokenHash));

        if (RevokedAt is not null)
        {
            return RefreshOutcome.SessionRevoked;
        }

        // Before the reuse, so that the outcome does not depend on whether the cleanup has deleted the token yet.
        if (token.IsExpiredAt(now))
        {
            return RefreshOutcome.TokenExpired;
        }

        if (token.ReplacedAt is not { } replacedAt)
        {
            token.Replace(now);
            _refreshTokens.Add(RefreshToken.Issue(newTokenHash, now));
            return RefreshOutcome.Rotated;
        }

        // The token stays replaced, and the new one joins the token that replaced it.
        if (now - replacedAt <= GraceWindow)
        {
            _refreshTokens.Add(RefreshToken.Issue(newTokenHash, now));
            return RefreshOutcome.ReissuedInGraceWindow;
        }

        Revoke(now);
        return RefreshOutcome.ReuseDetected;
    }

    /// <summary>Revokes the session, so that none of its tokens is accepted any more. A second call keeps the first time.</summary>
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
