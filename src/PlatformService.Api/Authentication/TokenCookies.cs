using PlatformService.Application.Abstractions;
using PlatformService.Application.Auth;

namespace PlatformService.Api.Authentication;

/// <summary>The two token cookies of ADR-4, which the gateway and the browser read.</summary>
internal static class TokenCookies
{
    /// <summary>The access token, which the browser sends to every path, also with the WebSocket handshake.</summary>
    public const string AccessTokenName = "__Host-access-token";

    /// <summary>The browser accepts a __Host- cookie only with this path, with Secure and without Domain.</summary>
    public const string AccessTokenPath = "/";

    /// <summary>The refresh token, which the browser sends only to the auth endpoints.</summary>
    public const string RefreshTokenName = "__Secure-refresh-token";

    public const string RefreshTokenPath = "/api/auth";

    /// <summary>Sets both cookies, each with the lifetime of its token as Max-Age.</summary>
    public static void Append(HttpResponse response, SignInResult result)
    {
        Append(response, AccessTokenName, result.AccessToken, AccessTokenPath);
        Append(response, RefreshTokenName, result.RefreshToken, RefreshTokenPath);
    }

    private static void Append(HttpResponse response, string name, IssuedToken token, string path) =>
        response.Cookies.Append(name, token.Value, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = path,
            MaxAge = token.Lifetime,
        });
}
