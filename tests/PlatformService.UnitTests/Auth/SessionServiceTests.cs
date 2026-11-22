using Microsoft.Extensions.Logging;
using PlatformService.Application.Abstractions;
using PlatformService.Application.Auth;
using PlatformService.Domain.Sessions;
using PlatformService.UnitTests.Fakes;

namespace PlatformService.UnitTests.Auth;

public sealed class SessionServiceTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    // What the fakes did, in order.
    private readonly List<string> _log = [];
    private readonly FakeUserAccounts _accounts;
    private readonly FakeSessionRepository _sessions;
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly ManualTimeProvider _clock = new(_start);
    private readonly FakeLogger<SessionService> _logger = new();
    private readonly SessionService _service;

    public SessionServiceTests()
    {
        _accounts = new FakeUserAccounts(_log);
        _sessions = new FakeSessionRepository(_log);
        _unitOfWork = new FakeUnitOfWork(_log);
        _service = new SessionService(_sessions, _accounts, new FakeAccessTokenIssuer(), _unitOfWork, _clock, _logger);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("an unknown token")]
    public async Task RefreshAsync_WithoutAKnownToken_EndsTheSessionWithoutACommit(string? refreshToken)
    {
        StartSession();

        var result = await _service.RefreshAsync(refreshToken, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task RefreshAsync_ReturnsANewAccessTokenAndANewRefreshToken()
    {
        var (account, session, refreshToken) = StartSession();
        _clock.Advance(TimeSpan.FromMinutes(15));

        var result = await _service.RefreshAsync(refreshToken, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(FakeAccessTokenIssuer.Token(account, _clock.GetUtcNow()), result.AccessToken.Value);
        Assert.NotEqual(refreshToken, result.RefreshToken.Value);
        Assert.Equal(TimeSpan.FromDays(7), result.RefreshToken.Lifetime);
        Assert.Contains(session.RefreshTokens, token => token.TokenHash.SequenceEqual(RefreshTokens.Hash(result.RefreshToken.Value)));
        Assert.Equal(["commit"], _log);
    }

    [Fact]
    public async Task RefreshAsync_WithATokenReplacedAtMost10SecondsAgo_ReturnsNewTokens()
    {
        var (_, session, refreshToken) = StartSession();
        await RefreshAsync(refreshToken);
        _clock.Advance(TimeSpan.FromSeconds(10));

        var result = await _service.RefreshAsync(refreshToken, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Null(session.RevokedAt);
    }

    [Fact]
    public async Task RefreshAsync_WithATokenReplacedEarlier_RevokesTheSessionAndLogsAWarning()
    {
        var (account, session, refreshToken) = StartSession();
        var newRefreshToken = await RefreshAsync(refreshToken);
        _clock.Advance(TimeSpan.FromSeconds(11));

        var result = await _service.RefreshAsync(refreshToken, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(_clock.GetUtcNow(), session.RevokedAt);
        Assert.Equal(["commit"], _log);
        var (level, message) = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, level);
        Assert.Contains(session.Id.ToString(), message, StringComparison.Ordinal);
        Assert.Contains(account.Id.ToString(), message, StringComparison.Ordinal);
        Assert.DoesNotContain(refreshToken, message, StringComparison.Ordinal);
        Assert.DoesNotContain(newRefreshToken, message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshAsync_AfterTheReuse_RefusesTheNewestTokenToo()
    {
        var (_, _, refreshToken) = StartSession();
        var newRefreshToken = await RefreshAsync(refreshToken);
        _clock.Advance(TimeSpan.FromSeconds(11));
        await _service.RefreshAsync(refreshToken, TestContext.Current.CancellationToken);

        var result = await _service.RefreshAsync(newRefreshToken, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task RefreshAsync_AfterAConflict_TriesOnceMoreAndGetsTheGraceWindow()
    {
        var (_, session, refreshToken) = StartSession();
        _unitOfWork.ConflictOnNextCommit = true;

        var result = await _service.RefreshAsync(refreshToken, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(["conflict", "commit"], _log);
        Assert.Null(session.RevokedAt);
        var newToken = Assert.Single(session.RefreshTokens, token => token.TokenHash.SequenceEqual(RefreshTokens.Hash(result.RefreshToken.Value)));
        Assert.Null(newToken.ReplacedAt);
    }

    [Fact]
    public async Task RefreshAsync_ForAUserDeletedMeanwhile_EndsTheSessionWithoutACommit()
    {
        var (account, _, refreshToken) = StartSession();
        _accounts.Remove(account.Id);

        var result = await _service.RefreshAsync(refreshToken, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task LogoutAsync_RevokesTheSessionSoThatItsTokenEndsIt()
    {
        var (_, session, refreshToken) = StartSession();

        await _service.LogoutAsync(refreshToken, TestContext.Current.CancellationToken);

        Assert.Equal(_start, session.RevokedAt);
        Assert.Equal(["commit"], _log);
        Assert.False((await _service.RefreshAsync(refreshToken, TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task LogoutAsync_WithAReplacedToken_RevokesTheSessionToo()
    {
        var (_, session, refreshToken) = StartSession();
        await RefreshAsync(refreshToken);
        _clock.Advance(TimeSpan.FromMinutes(1));

        await _service.LogoutAsync(refreshToken, TestContext.Current.CancellationToken);

        Assert.Equal(_clock.GetUtcNow(), session.RevokedAt);
        Assert.Empty(_logger.Entries);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("an unknown token")]
    public async Task LogoutAsync_WithoutAKnownToken_ChangesNothing(string? refreshToken)
    {
        var (_, session, _) = StartSession();

        await _service.LogoutAsync(refreshToken, TestContext.Current.CancellationToken);

        Assert.Null(session.RevokedAt);
        Assert.Empty(_log);
    }

    /// <summary>A session that a sign-in started earlier, with the refresh token of its cookie.</summary>
    private (UserAccount Account, Session Session, string RefreshToken) StartSession()
    {
        var account = _accounts.Seed("alice", "alice@example.com", "correct horse battery");
        var (value, hash) = RefreshTokens.Create();
        var session = Session.Start(account.Id, hash, _clock.GetUtcNow());
        _sessions.Added.Add(session);
        return (account, session, value);
    }

    /// <summary>Refreshes successfully, and returns the new refresh token while the log stays empty.</summary>
    private async Task<string> RefreshAsync(string refreshToken)
    {
        var result = await _service.RefreshAsync(refreshToken, TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded);
        _log.Clear();
        return result.RefreshToken.Value;
    }
}
