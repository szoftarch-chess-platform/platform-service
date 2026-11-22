using Microsoft.Net.Http.Headers;

namespace PlatformService.IntegrationTests.Auth;

/// <summary>The attributes of the token cookies of ADR-4, as the contract shows them.</summary>
internal static class TokenCookieAssert
{
    public static void Set(SetCookieHeaderValue cookie, string path, TimeSpan maxAge)
    {
        AssertAttributes(cookie, path);
        Assert.Equal(maxAge, cookie.MaxAge);
        Assert.False(cookie.Value.Length == 0);
    }

    /// <summary>Both cookies of the response are set with the lifetimes of ADR-4.</summary>
    public static void BothSet(HttpResponseMessage response)
    {
        Set(AuthClient.Cookie(response, AuthClient.AccessTokenCookie), "/", TimeSpan.FromSeconds(900));
        Set(AuthClient.Cookie(response, AuthClient.RefreshTokenCookie), "/api/auth", TimeSpan.FromSeconds(604800));
    }

    /// <summary>
    /// Both cookies of the response are deleted with an expiry in the past. The browser applies a deletion only with
    /// the Path and the Secure of the cookie.
    /// </summary>
    public static void BothDeleted(HttpResponseMessage response)
    {
        Deleted(AuthClient.Cookie(response, AuthClient.AccessTokenCookie), "/");
        Deleted(AuthClient.Cookie(response, AuthClient.RefreshTokenCookie), "/api/auth");
    }

    private static void Deleted(SetCookieHeaderValue cookie, string path)
    {
        AssertAttributes(cookie, path);
        Assert.True(cookie.Value.Length == 0, "A deleted cookie has no value.");
        Assert.True(cookie.Expires < DateTimeOffset.UtcNow, "A deleted cookie expires in the past.");
        Assert.Null(cookie.MaxAge);
    }

    private static void AssertAttributes(SetCookieHeaderValue cookie, string path)
    {
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(SameSiteMode.Strict, cookie.SameSite);
        Assert.Equal(path, cookie.Path.ToString());
        Assert.True(cookie.Domain.Length == 0, "A __Host- cookie must have no Domain, and neither has the other one.");
    }
}
