using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;

namespace PlatformService.IntegrationTests.Auth;

/// <summary>Calls the auth endpoints and reads their cookies from the raw Set-Cookie headers.</summary>
internal static class AuthClient
{
    public const string Password = "correct horse battery";

    public const string AccessTokenCookie = "__Host-access-token";

    public const string RefreshTokenCookie = "__Secure-refresh-token";

    /// <summary>The client keeps no cookies, so every test sees exactly what one response sets.</summary>
    public static HttpClient Create(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    public static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string userName, string? email = null, string password = Password) =>
        client.PostAsJsonAsync(
            "/api/auth/register",
            new { userName, email = email ?? $"{userName}@example.com", password },
            TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string userName, string password = Password) =>
        client.PostAsJsonAsync("/api/auth/login", new { userName, password }, TestContext.Current.CancellationToken);

    /// <summary>Sends the refresh token in its cookie as the browser does, or no cookie at all.</summary>
    public static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string? refreshToken) =>
        PostWithRefreshTokenAsync(client, "/api/auth/refresh", refreshToken);

    /// <summary>Sends the refresh token in its cookie as the browser does, or no cookie at all.</summary>
    public static Task<HttpResponseMessage> LogoutAsync(HttpClient client, string? refreshToken) =>
        PostWithRefreshTokenAsync(client, "/api/auth/logout", refreshToken);

    public static SetCookieHeaderValue Cookie(HttpResponseMessage response, string name)
    {
        var cookies = SetCookieHeaderValue.ParseList(response.Headers.GetValues(HeaderNames.SetCookie).ToList());
        return Assert.Single(cookies, cookie => cookie.Name.Equals(name));
    }

    /// <summary>The refresh token that the response sets.</summary>
    public static string RefreshToken(HttpResponseMessage response) => Cookie(response, RefreshTokenCookie).Value.ToString();

    /// <summary>The hash of a refresh token, by which the database knows it (PS-ADR-12).</summary>
    public static byte[] Hash(string refreshToken) => SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));

    private static async Task<HttpResponseMessage> PostWithRefreshTokenAsync(HttpClient client, string path, string? refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (refreshToken is not null)
        {
            request.Headers.Add(HeaderNames.Cookie, $"{RefreshTokenCookie}={refreshToken}");
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
