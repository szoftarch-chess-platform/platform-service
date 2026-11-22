using System.Net.Http.Json;
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

    public static SetCookieHeaderValue Cookie(HttpResponseMessage response, string name)
    {
        var cookies = SetCookieHeaderValue.ParseList(response.Headers.GetValues(HeaderNames.SetCookie).ToList());
        return Assert.Single(cookies, cookie => cookie.Name.Equals(name));
    }
}
