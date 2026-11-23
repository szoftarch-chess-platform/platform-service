using PlatformService.Application.Abstractions;

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
    public static void Append(HttpResponse response, IssuedToken accessToken, IssuedToken refreshToken)
    {
        Append(response, AccessTokenName, accessToken, AccessTokenPath);
        Append(response, RefreshTokenName, refreshToken, RefreshTokenPath);
    }

    /// <summary>
    /// Deletes both cookies with an expiry in the past. The browser drops the deletion of a cookie whose Path differs,
    /// and of a prefixed cookie without Secure, so it gets the attributes that the cookie was set with.
    /// </summary>
    public static void Delete(HttpResponse response)
    {
        response.Cookies.Delete(AccessTokenName, CreateOptions(AccessTokenPath));
        response.Cookies.Delete(RefreshTokenName, CreateOptions(RefreshTokenPath));
    }

    private static void Append(HttpResponse response, string name, IssuedToken token, string path)
    {
        var options = CreateOptions(path);
        options.MaxAge = token.Lifetime;
        response.Cookies.Append(name, token.Value, options);
    }

    private static CookieOptions CreateOptions(string path) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = path,
    };
}
