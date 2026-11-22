using System.Net;
using Microsoft.Net.Http.Headers;
using static PlatformService.IntegrationTests.TestNames;

namespace PlatformService.IntegrationTests.Auth;

/// <summary>Registration and sign-in as the contract <c>platform-service.openapi.yaml</c> describes them.</summary>
public sealed class AuthEndpointTests(PlatformApiFactory factory) : IClassFixture<PlatformApiFactory>
{
    [Fact]
    public async Task Register_Returns201WithTheNewUser()
    {
        using var client = AuthClient.Create(factory);
        var userName = NewUserName();

        using var response = await AuthClient.RegisterAsync(client, userName);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.ReadJsonAsync();
        Assert.Equal(7, user.GetRequiredGuid("id").Version);
        Assert.Equal(userName, user.GetProperty("userName").GetString());
    }

    [Fact]
    public async Task Register_SetsTheAccessTokenCookieOfAdr4()
    {
        using var client = AuthClient.Create(factory);

        using var response = await AuthClient.RegisterAsync(client, NewUserName());

        var cookie = AuthClient.Cookie(response, AuthClient.AccessTokenCookie);
        TokenCookieAssert.Set(cookie, path: "/", maxAge: TimeSpan.FromSeconds(900));
    }

    [Fact]
    public async Task Register_SetsTheRefreshTokenCookieOfAdr4()
    {
        using var client = AuthClient.Create(factory);

        using var response = await AuthClient.RegisterAsync(client, NewUserName());

        var cookie = AuthClient.Cookie(response, AuthClient.RefreshTokenCookie);
        TokenCookieAssert.Set(cookie, path: "/api/auth", maxAge: TimeSpan.FromSeconds(604800));
    }

    [Fact]
    public async Task Register_StartsASessionWithOnlyTheHashOfTheRefreshToken()
    {
        using var client = AuthClient.Create(factory);

        using var response = await AuthClient.RegisterAsync(client, NewUserName());

        var userId = (await response.ReadJsonAsync()).GetRequiredGuid("id");
        var refreshToken = AuthClient.RefreshToken(response);
        var token = Assert.Single(Assert.Single(await StoredSessions.OfUserAsync(factory, userId)).RefreshTokens);
        Assert.Equal(AuthClient.Hash(refreshToken), token.TokenHash);
        Assert.Equal(token.IssuedAt.AddDays(7), token.ExpiresAt);
    }

    [Fact]
    public async Task Register_WithATakenUserNameInAnotherCase_Returns409WithTheField()
    {
        using var client = AuthClient.Create(factory);
        var userName = NewUserName();
        (await AuthClient.RegisterAsync(client, userName)).Dispose();

        using var response = await AuthClient.RegisterAsync(client, userName.ToUpperInvariant(), $"{NewUserName()}@example.com");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.ReadJsonAsync();
        Assert.Equal("The user name is already taken.", problem.GetProperty("detail").GetString());
        var errors = problem.GetProperty("errors");
        Assert.Equal(["userName"], errors.EnumerateObject().Select(property => property.Name));
        Assert.Equal(["The user name is already taken."], errors.GetProperty("userName").EnumerateArray().Select(message => message.GetString()));
    }

    [Fact]
    public async Task Register_WithATakenUserNameAndEmail_Returns409WithBothFields()
    {
        using var client = AuthClient.Create(factory);
        var userName = NewUserName();
        (await AuthClient.RegisterAsync(client, userName)).Dispose();

        using var response = await AuthClient.RegisterAsync(client, userName, $"{userName}@EXAMPLE.com");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.ReadJsonAsync();
        Assert.Equal("The user name and the email address are already taken.", problem.GetProperty("detail").GetString());
        Assert.Equal(["userName", "email"], problem.GetProperty("errors").EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public async Task Register_WithAnInvalidRequest_Returns400WithTheMessagesByField()
    {
        using var client = AuthClient.Create(factory);

        using var response = await AuthClient.RegisterAsync(client, "al", "alice@example.com", "1234567");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        Assert.Equal(["The user name must be 3 to 20 characters long."], errors.GetProperty("userName").EnumerateArray().Select(message => message.GetString()));
        Assert.Equal(["The password must be at least 8 characters long."], errors.GetProperty("password").EnumerateArray().Select(message => message.GetString()));
        Assert.False(response.Headers.Contains(HeaderNames.SetCookie));
    }

    [Fact]
    public async Task Login_WithTheRightPasswordAndTheUserNameInAnotherCase_Returns200AndBothCookies()
    {
        using var client = AuthClient.Create(factory);
        var userName = NewUserName();
        (await AuthClient.RegisterAsync(client, userName)).Dispose();

        using var response = await AuthClient.LoginAsync(client, userName.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(userName, (await response.ReadJsonAsync()).GetProperty("userName").GetString());
        TokenCookieAssert.BothSet(response);
    }

    [Fact]
    public async Task Login_StartsANewSessionNextToTheOneOfTheRegistration()
    {
        using var client = AuthClient.Create(factory);
        using var registration = await AuthClient.RegisterAsync(client, NewUserName());
        var user = await registration.ReadJsonAsync();

        (await AuthClient.LoginAsync(client, user.GetRequiredString("userName"))).Dispose();

        Assert.Equal(2, (await StoredSessions.OfUserAsync(factory, user.GetRequiredGuid("id"))).Count);
    }

    [Theory]
    [InlineData(true, "wrong horse battery")]
    [InlineData(false, AuthClient.Password)]
    public async Task Login_WithWrongCredentials_Returns401WithTheSameDetail(bool knownUser, string password)
    {
        using var client = AuthClient.Create(factory);
        var userName = NewUserName();
        if (knownUser)
        {
            (await AuthClient.RegisterAsync(client, userName)).Dispose();
        }

        using var response = await AuthClient.LoginAsync(client, userName, password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("The user name or the password is wrong.", (await response.ReadJsonAsync()).GetProperty("detail").GetString());
        Assert.False(response.Headers.Contains(HeaderNames.SetCookie));
    }

    [Fact]
    public async Task Login_WithAMissingPassword_Returns400()
    {
        using var client = AuthClient.Create(factory);

        using var response = await AuthClient.LoginAsync(client, NewUserName(), string.Empty);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        Assert.Equal(["The password is required."], errors.GetProperty("password").EnumerateArray().Select(message => message.GetString()));
    }
}
