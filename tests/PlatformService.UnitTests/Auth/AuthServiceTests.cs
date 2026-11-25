using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using PlatformService.Application.Auth;
using PlatformService.Application.Common.Exceptions;
using PlatformService.Application.Users;
using PlatformService.UnitTests.Fakes;

namespace PlatformService.UnitTests.Auth;

public sealed class AuthServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    // What the fakes did, in order.
    private readonly List<string> _log = [];
    private readonly FakeUserAccounts _accounts;
    private readonly FakeSessionRepository _sessions;
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _accounts = new FakeUserAccounts(_log);
        _sessions = new FakeSessionRepository(_log);
        _service = new AuthService(
            new RegisterRequestValidator(),
            new LoginRequestValidator(),
            _accounts,
            _sessions,
            new FakeAccessTokenIssuer(),
            new FakeUnitOfWork(_log),
            new ManualTimeProvider(_now));
    }

    [Fact]
    public async Task RegisterAsync_SavesTheAccountAndItsSessionInOneCommit()
    {
        await _service.RegisterAsync(Registration("alice"), TestContext.Current.CancellationToken);

        Assert.Equal(["create alice", "add session", "commit"], _log);
    }

    [Fact]
    public async Task RegisterAsync_GivesTheAccountAUuidV7WithTheCurrentTime()
    {
        var result = await _service.RegisterAsync(Registration("alice"), TestContext.Current.CancellationToken);

        Assert.Equal(7, result.User.Id.Version);
        Assert.Equal(_now.ToUnixTimeMilliseconds(), UuidV7.UnixMilliseconds(result.User.Id));
    }

    [Fact]
    public async Task RegisterAsync_ReturnsTheUserWithBothTokens()
    {
        var result = await _service.RegisterAsync(Registration("alice"), TestContext.Current.CancellationToken);

        Assert.Equal("alice", result.User.UserName);
        Assert.Equal(FakeAccessTokenIssuer.Token(new(result.User.Id, "alice"), _now), result.AccessToken.Value);
        Assert.Equal(FakeAccessTokenIssuer.Lifetime, result.AccessToken.Lifetime);
        Assert.Equal(TimeSpan.FromDays(7), result.RefreshToken.Lifetime);
    }

    [Fact]
    public async Task RegisterAsync_StartsASessionWithTheHashOfTheReturnedRefreshToken()
    {
        var result = await _service.RegisterAsync(Registration("alice"), TestContext.Current.CancellationToken);

        var session = Assert.Single(_sessions.Added);
        Assert.Equal(result.User.Id, session.UserId);
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(result.RefreshToken.Value));
        Assert.Equal(expectedHash, Assert.Single(session.RefreshTokens).TokenHash);
    }

    [Fact]
    public async Task RegisterAsync_WithAnInvalidRequest_ChangesNothing()
    {
        var request = new RegisterRequest { UserName = "al", Email = "alice@example.com", Password = "correct horse battery" };

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.RegisterAsync(request, TestContext.Current.CancellationToken));

        Assert.Empty(_log);
    }

    [Fact]
    public async Task RegisterAsync_WithATakenUserName_StartsNoSession()
    {
        _accounts.Seed("alice", "other@example.com", "correct horse battery");

        var exception = await Assert.ThrowsAsync<AccountTakenException>(
            () => _service.RegisterAsync(Registration("ALICE"), TestContext.Current.CancellationToken));

        Assert.Equal(TakenAccountFields.UserName, exception.Fields);
        Assert.Empty(_log);
    }

    [Fact]
    public async Task LoginAsync_WithTheRightPassword_StartsASessionForTheAccount()
    {
        var account = _accounts.Seed("alice", "alice@example.com", "correct horse battery");

        var result = await _service.LoginAsync(
            new LoginRequest { UserName = "alice", Password = "correct horse battery" },
            TestContext.Current.CancellationToken);

        Assert.Equal(new CurrentUserDto(account.Id, "alice"), result.User);
        Assert.Equal(account.Id, Assert.Single(_sessions.Added).UserId);
        Assert.Equal(["add session", "commit"], _log);
    }

    [Theory]
    [InlineData("alice", "wrong horse battery")]
    [InlineData("bob", "correct horse battery")]
    public async Task LoginAsync_WithWrongCredentials_ThrowsTheSameMessageAndChangesNothing(string userName, string password)
    {
        _accounts.Seed("alice", "alice@example.com", "correct horse battery");

        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _service.LoginAsync(new LoginRequest { UserName = userName, Password = password }, TestContext.Current.CancellationToken));

        Assert.Equal(AuthService.WrongCredentials, exception.Message);
        Assert.Empty(_log);
    }

    private static RegisterRequest Registration(string userName) =>
        new() { UserName = userName, Email = $"{userName}@example.com", Password = "correct horse battery" };
}
