using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static PlatformService.IntegrationTests.TestNames;

namespace PlatformService.IntegrationTests.Auth;

/// <summary>
/// The access token as the gateway sees it. The checks repeat by hand what Envoy and the scripts of the infra repo do,
/// so they do not depend on the library that signs the token (ADR-4, PS-ADR-12).
/// </summary>
public sealed class AccessTokenTests(PlatformApiFactory factory, SigningKeyFixture signingKey) : IClassFixture<PlatformApiFactory>
{
    [Fact]
    public async Task AccessToken_IsSignedWithEs256ByTheKeyThatItsKidNames()
    {
        var (header, _, signingInput, signature) = await RegisterAndReadAccessTokenAsync(NewUserName());

        Assert.Equal("ES256", header.GetProperty("alg").GetString());
        Assert.Equal("JWT", header.GetProperty("typ").GetString());
        Assert.Equal(Rfc7638Thumbprint(signingKey.Key), header.GetProperty("kid").GetString());

        // JWS carries the two numbers of the ECDSA signature side by side, which is the default of .NET too.
        Assert.True(signingKey.Key.VerifyData(signingInput, signature, HashAlgorithmName.SHA256));
    }

    [Fact]
    public async Task AccessToken_HasExactlyTheClaimsOfAdr4()
    {
        var userName = NewUserName();

        var (_, payload, _, _) = await RegisterAndReadAccessTokenAsync(userName);

        Assert.Equal(
            ["aud", "exp", "iat", "iss", "preferred_username", "sub"],
            payload.EnumerateObject().Select(claim => claim.Name).Order(StringComparer.Ordinal));
        Assert.Equal("platform-service", payload.GetProperty("iss").GetString());
        Assert.Equal("chess-platform", payload.GetProperty("aud").GetString());
        Assert.Equal(userName, payload.GetProperty("preferred_username").GetString());
        Assert.Equal(7, payload.GetRequiredGuid("sub").Version);
        Assert.Equal(900, payload.GetProperty("exp").GetInt64() - payload.GetProperty("iat").GetInt64());
    }

    [Fact]
    public async Task AccessToken_HasTheIdOfTheResponseAsItsSubject()
    {
        using var client = AuthClient.Create(factory);
        using var response = await AuthClient.RegisterAsync(client, NewUserName());

        var payload = Decode(AuthClient.Cookie(response, AuthClient.AccessTokenCookie).Value.ToString().Split('.')[1]);

        Assert.Equal((await response.ReadJsonAsync()).GetProperty("id").GetString(), payload.GetProperty("sub").GetString());
    }

    /// <summary>
    /// The JWK thumbprint of the public key, which is the kid of the JWKS that the script of the infra repo writes.
    /// </summary>
    private static string Rfc7638Thumbprint(ECDsa key)
    {
        var point = key.ExportParameters(includePrivateParameters: false).Q;
        var members = $$"""{"crv":"P-256","kty":"EC","x":"{{Base64Url.EncodeToString(point.X)}}","y":"{{Base64Url.EncodeToString(point.Y)}}"}""";
        return Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(members)));
    }

    private static JsonElement Decode(string part)
    {
        using var document = JsonDocument.Parse(Base64Url.DecodeFromChars(part));
        return document.RootElement.Clone();
    }

    private async Task<(JsonElement Header, JsonElement Payload, byte[] SigningInput, byte[] Signature)> RegisterAndReadAccessTokenAsync(string userName)
    {
        using var client = AuthClient.Create(factory);
        using var response = await AuthClient.RegisterAsync(client, userName);

        var parts = AuthClient.Cookie(response, AuthClient.AccessTokenCookie).Value.ToString().Split('.');
        Assert.Equal(3, parts.Length);
        return (
            Decode(parts[0]),
            Decode(parts[1]),
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
            Base64Url.DecodeFromChars(parts[2]));
    }
}
