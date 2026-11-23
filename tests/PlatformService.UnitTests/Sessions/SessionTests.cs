using PlatformService.Domain.Sessions;

namespace PlatformService.UnitTests.Sessions;

public sealed class SessionTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] _first = [1];
    private static readonly byte[] _second = [2];
    private static readonly byte[] _third = [3];

    [Fact]
    public void Start_CreatesTheSessionWithItsFirstRefreshToken()
    {
        var userId = Guid.CreateVersion7();
        byte[] hash = [1, 2, 3];

        var session = Session.Start(userId, hash, _now);

        Assert.Equal(userId, session.UserId);
        Assert.Equal(_now, session.StartedAt);
        Assert.Null(session.RevokedAt);
        var token = Assert.Single(session.RefreshTokens);
        Assert.Equal(hash, token.TokenHash);
        Assert.Equal(_now, token.IssuedAt);
        Assert.Null(token.ReplacedAt);
    }

    [Fact]
    public void Start_GivesTheRefreshTokenALifetimeOf7Days()
    {
        var session = Session.Start(Guid.CreateVersion7(), [1], _now);

        Assert.Equal(_now.AddDays(7), Assert.Single(session.RefreshTokens).ExpiresAt);
    }

    [Fact]
    public void Start_CreatesUuidV7IdsWithTheTimeOfTheStart()
    {
        var session = Session.Start(Guid.CreateVersion7(), [1], _now);

        Assert.Equal(7, session.Id.Version);
        Assert.Equal(_now.ToUnixTimeMilliseconds(), UuidV7.UnixMilliseconds(session.Id));
        Assert.Equal(_now.ToUnixTimeMilliseconds(), UuidV7.UnixMilliseconds(Assert.Single(session.RefreshTokens).Id));
    }

    [Fact]
    public void Refresh_WithATokenNotReplacedYet_ReplacesItWithANewToken()
    {
        var session = Session.Start(Guid.CreateVersion7(), _first, _now);
        var later = _now.AddMinutes(15);

        var outcome = session.Refresh(_first, _second, later);

        Assert.Equal(RefreshOutcome.Rotated, outcome);
        Assert.Equal(later, Token(session, _first).ReplacedAt);
        var newToken = Token(session, _second);
        Assert.Equal(later, newToken.IssuedAt);
        Assert.Equal(later.AddDays(7), newToken.ExpiresAt);
        Assert.Null(newToken.ReplacedAt);
        Assert.Null(session.RevokedAt);
    }

    [Fact]
    public void Refresh_WithTheNewToken_RotatesAgainWith7MoreDays()
    {
        var session = Session.Start(Guid.CreateVersion7(), _first, _now);
        session.Refresh(_first, _second, _now.AddDays(6));

        var outcome = session.Refresh(_second, _third, _now.AddDays(12));

        Assert.Equal(RefreshOutcome.Rotated, outcome);
        Assert.Equal(_now.AddDays(19), Token(session, _third).ExpiresAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_000)]
    public void Refresh_WithATokenReplacedAtMost10SecondsAgo_AddsAnotherNewTokenAndRevokesNothing(int milliseconds)
    {
        var session = Session.Start(Guid.CreateVersion7(), _first, _now);
        session.Refresh(_first, _second, _now);

        var outcome = session.Refresh(_first, _third, _now.AddMilliseconds(milliseconds));

        Assert.Equal(RefreshOutcome.ReissuedInGraceWindow, outcome);
        Assert.Null(session.RevokedAt);
        Assert.Equal(_now, Token(session, _first).ReplacedAt);
        Assert.Null(Token(session, _second).ReplacedAt);
        Assert.Null(Token(session, _third).ReplacedAt);
    }

    [Fact]
    public void Refresh_WithATokenReplacedMoreThan10SecondsAgo_RevokesTheSession()
    {
        var session = Session.Start(Guid.CreateVersion7(), _first, _now);
        session.Refresh(_first, _second, _now);
        var later = _now.AddMilliseconds(10_001);

        var outcome = session.Refresh(_first, _third, later);

        Assert.Equal(RefreshOutcome.ReuseDetected, outcome);
        Assert.Equal(later, session.RevokedAt);
        Assert.DoesNotContain(session.RefreshTokens, token => token.TokenHash.SequenceEqual(_third));
    }

    [Fact]
    public void Refresh_OfARevokedSession_IssuesNoTokenEvenForTheNewest()
    {
        var session = Session.Start(Guid.CreateVersion7(), _first, _now);
        session.Refresh(_first, _second, _now);
        session.Revoke(_now.AddMinutes(1));

        var outcome = session.Refresh(_second, _third, _now.AddMinutes(2));

        Assert.Equal(RefreshOutcome.SessionRevoked, outcome);
        Assert.Equal(2, session.RefreshTokens.Count);
    }

    [Fact]
    public void Refresh_WithATokenAtItsExpiry_IsRefused()
    {
        var session = Session.Start(Guid.CreateVersion7(), _first, _now);

        var outcome = session.Refresh(_first, _second, _now.AddDays(7));

        Assert.Equal(RefreshOutcome.TokenExpired, outcome);
        Assert.Single(session.RefreshTokens);
    }

    [Fact]
    public void Refresh_WithAnExpiredReplacedToken_RevokesNothing()
    {
        var session = Session.Start(Guid.CreateVersion7(), _first, _now);
        session.Refresh(_first, _second, _now.AddMinutes(1));

        var outcome = session.Refresh(_first, _third, _now.AddDays(8));

        Assert.Equal(RefreshOutcome.TokenExpired, outcome);
        Assert.Null(session.RevokedAt);
    }

    [Fact]
    public void Refresh_WithATokenThatTheSessionWasLoadedWithout_Throws()
    {
        var session = Session.Start(Guid.CreateVersion7(), _first, _now);

        Assert.Throws<ArgumentException>(() => session.Refresh(_second, _third, _now));
    }

    [Fact]
    public void Revoke_KeepsTheTimeOfTheFirstRevocation()
    {
        var session = Session.Start(Guid.CreateVersion7(), _first, _now);
        session.Revoke(_now.AddMinutes(1));

        session.Revoke(_now.AddMinutes(2));

        Assert.Equal(_now.AddMinutes(1), session.RevokedAt);
    }

    private static RefreshToken Token(Session session, byte[] hash) =>
        Assert.Single(session.RefreshTokens, token => token.TokenHash.SequenceEqual(hash));
}
