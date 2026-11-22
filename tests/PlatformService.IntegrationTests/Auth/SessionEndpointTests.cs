using System.Net;
using Microsoft.Net.Http.Headers;
using static PlatformService.IntegrationTests.TestNames;

namespace PlatformService.IntegrationTests.Auth;

/// <summary>Refresh and sign-out as the contract <c>platform-service.openapi.yaml</c> describes them.</summary>
public sealed class SessionEndpointTests : IClassFixture<PlatformApiFactory>, IDisposable
{
    private const string SessionEnded = "The session has ended. Sign in again.";

    private readonly PlatformApiFactory _factory;
    private readonly HttpClient _client;

    public SessionEndpointTests(PlatformApiFactory factory)
    {
        // The tests of a class run one after the other, so each one starts at the real time.
        factory.Clock.Reset();
        _factory = factory;
        _client = AuthClient.Create(factory);
    }

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Refresh_WithTheCookie_Returns204AndRenewsBothCookies()
    {
        var (_, refreshToken) = await RegisterAsync();

        using var response = await AuthClient.RefreshAsync(_client, refreshToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        TokenCookieAssert.BothSet(response);
        Assert.NotEqual(refreshToken, AuthClient.RefreshToken(response));
    }

    [Fact]
    public async Task Refresh_ReplacesTheTokenWithANewOneInTheSession()
    {
        var (userId, refreshToken) = await RegisterAsync();

        var newRefreshToken = await RefreshAsync(refreshToken);

        var session = Assert.Single(await StoredSessions.OfUserAsync(_factory, userId));
        Assert.NotNull(Assert.Single(session.RefreshTokens, token => token.TokenHash.SequenceEqual(AuthClient.Hash(refreshToken))).ReplacedAt);
        Assert.Null(Assert.Single(session.RefreshTokens, token => token.TokenHash.SequenceEqual(AuthClient.Hash(newRefreshToken))).ReplacedAt);
    }

    [Fact]
    public async Task Refresh_EveryFewDays_KeepsTheSessionBeyond7Days()
    {
        var (_, refreshToken) = await RegisterAsync();
        _factory.Clock.Advance(TimeSpan.FromDays(6));
        var newRefreshToken = await RefreshAsync(refreshToken);
        _factory.Clock.Advance(TimeSpan.FromDays(6));

        using var response = await AuthClient.RefreshAsync(_client, newRefreshToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithATokenReplacedAtMost10SecondsAgo_Returns204AndKeepsEveryNewToken()
    {
        var (_, refreshToken) = await RegisterAsync();
        var newRefreshToken = await RefreshAsync(refreshToken);

        // The real time passes too, so the exact limit of 10 seconds is left to the tests of the Domain.
        _factory.Clock.Advance(TimeSpan.FromSeconds(9));

        using var response = await AuthClient.RefreshAsync(_client, refreshToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        TokenCookieAssert.BothSet(response);
        await RefreshAsync(newRefreshToken);
        await RefreshAsync(AuthClient.RefreshToken(response));
    }

    [Fact]
    public async Task Refresh_WithATokenReplacedMoreThan10SecondsAgo_Returns401AndRevokesTheSession()
    {
        var (userId, refreshToken) = await RegisterAsync();
        var newRefreshToken = await RefreshAsync(refreshToken);
        _factory.Clock.Advance(TimeSpan.FromSeconds(11));

        using var response = await AuthClient.RefreshAsync(_client, refreshToken);

        await AssertSessionEndedAsync(response);
        Assert.NotNull(Assert.Single(await StoredSessions.OfUserAsync(_factory, userId)).RevokedAt);
        using var withNewToken = await AuthClient.RefreshAsync(_client, newRefreshToken);
        await AssertSessionEndedAsync(withNewToken);
    }

    [Fact]
    public async Task Refresh_WithoutTheCookie_Returns401ProblemAndDeletesBothCookies()
    {
        using var response = await AuthClient.RefreshAsync(_client, refreshToken: null);

        await AssertSessionEndedAsync(response);
        var problem = await response.ReadJsonAsync();
        Assert.Equal("Unauthorized", problem.GetRequiredString("title"));
        Assert.Equal(401, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.GetRequiredString("traceId")));
        Assert.False(string.IsNullOrEmpty(problem.GetRequiredString("correlationId")));
    }

    [Fact]
    public async Task Refresh_WithAnUnknownToken_Returns401()
    {
        using var response = await AuthClient.RefreshAsync(_client, "an-unknown-token");

        await AssertSessionEndedAsync(response);
    }

    [Fact]
    public async Task Refresh_After7DaysWithoutARefresh_Returns401()
    {
        var (_, refreshToken) = await RegisterAsync();
        _factory.Clock.Advance(TimeSpan.FromDays(7));

        using var response = await AuthClient.RefreshAsync(_client, refreshToken);

        await AssertSessionEndedAsync(response);
    }

    [Fact]
    public async Task Refresh_InParallelWithTheSameToken_EndsNoSession()
    {
        var (userId, refreshToken) = await RegisterAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => AuthClient.RefreshAsync(_client, refreshToken)));

        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
            var newRefreshTokens = responses.Select(AuthClient.RefreshToken).ToList();
            Assert.Equal(newRefreshTokens.Count, newRefreshTokens.Distinct(StringComparer.Ordinal).Count());
            Assert.Null(Assert.Single(await StoredSessions.OfUserAsync(_factory, userId)).RevokedAt);
            foreach (var newRefreshToken in newRefreshTokens)
            {
                await RefreshAsync(newRefreshToken);
            }
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Logout_RevokesTheSessionAndDeletesBothCookies()
    {
        var (_, refreshToken) = await RegisterAsync();

        using var response = await AuthClient.LogoutAsync(_client, refreshToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        TokenCookieAssert.BothDeleted(response);
        using var refresh = await AuthClient.RefreshAsync(_client, refreshToken);
        await AssertSessionEndedAsync(refresh);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("an-unknown-token")]
    public async Task Logout_WithoutAValidToken_Returns204AndDeletesBothCookies(string? refreshToken)
    {
        using var response = await AuthClient.LogoutAsync(_client, refreshToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        TokenCookieAssert.BothDeleted(response);
        Assert.True(response.Content.Headers.ContentLength is null or 0);
    }

    [Fact]
    public async Task Logout_KeepsTheOtherSessionsOfTheUser()
    {
        var userName = NewUserName();
        using var registration = await AuthClient.RegisterAsync(_client, userName);
        using var login = await AuthClient.LoginAsync(_client, userName);

        (await AuthClient.LogoutAsync(_client, AuthClient.RefreshToken(registration))).Dispose();

        await RefreshAsync(AuthClient.RefreshToken(login));
    }

    private async Task<(Guid UserId, string RefreshToken)> RegisterAsync()
    {
        using var response = await AuthClient.RegisterAsync(_client, NewUserName());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await response.ReadJsonAsync()).GetRequiredGuid("id"), AuthClient.RefreshToken(response));
    }

    /// <summary>Refreshes successfully, and returns the new refresh token.</summary>
    private async Task<string> RefreshAsync(string refreshToken)
    {
        using var response = await AuthClient.RefreshAsync(_client, refreshToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return AuthClient.RefreshToken(response);
    }

    private static async Task AssertSessionEndedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(SessionEnded, (await response.ReadJsonAsync()).GetRequiredString("detail"));
        TokenCookieAssert.BothDeleted(response);
        Assert.Equal(2, response.Headers.GetValues(HeaderNames.SetCookie).Count());
    }
}
