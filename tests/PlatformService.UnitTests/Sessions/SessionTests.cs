using PlatformService.Domain.Sessions;

namespace PlatformService.UnitTests.Sessions;

public sealed class SessionTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Start_CreatesTheSessionWithItsFirstRefreshToken()
    {
        var userId = Guid.CreateVersion7();
        byte[] hash = [1, 2, 3];

        var session = Session.Start(userId, hash, _now);

        Assert.Equal(userId, session.UserId);
        Assert.Equal(_now, session.StartedAt);
        var token = Assert.Single(session.RefreshTokens);
        Assert.Equal(hash, token.TokenHash);
        Assert.Equal(_now, token.IssuedAt);
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
}
