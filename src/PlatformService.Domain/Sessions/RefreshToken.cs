namespace PlatformService.Domain.Sessions;

/// <summary>A refresh token of a <see cref="Session"/>, stored as its hash.</summary>
public sealed class RefreshToken
{
    private RefreshToken(Guid id, byte[] tokenHash, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        Id = id;
        TokenHash = tokenHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>How long a refresh token is valid from its issue (ADR-4).</summary>
    public static TimeSpan Lifetime { get; } = TimeSpan.FromDays(7);

    public Guid Id { get; }

    public byte[] TokenHash { get; }

    public DateTimeOffset IssuedAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    /// <summary>When a refresh replaced the token with a new one, or <see langword="null"/> while it is the newest.</summary>
    public DateTimeOffset? ReplacedAt { get; private set; }

    internal static RefreshToken Issue(byte[] tokenHash, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);
        return new RefreshToken(Guid.CreateVersion7(now), tokenHash, now, now + Lifetime);
    }

    internal bool IsExpiredAt(DateTimeOffset now) => ExpiresAt <= now;

    internal void Replace(DateTimeOffset now) => ReplacedAt = now;
}
