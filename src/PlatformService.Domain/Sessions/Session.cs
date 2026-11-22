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

    public Guid Id { get; }

    public Guid UserId { get; }

    public DateTimeOffset StartedAt { get; }

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens;

    /// <summary>Starts the session of a registration or a sign-in with its first refresh token.</summary>
    /// <param name="refreshTokenHash">The SHA-256 hash of the refresh token. The session never holds the token itself.</param>
    public static Session Start(Guid userId, byte[] refreshTokenHash, DateTimeOffset now)
    {
        var session = new Session(Guid.CreateVersion7(now), userId, now);
        session._refreshTokens.Add(RefreshToken.Issue(refreshTokenHash, now));
        return session;
    }
}
