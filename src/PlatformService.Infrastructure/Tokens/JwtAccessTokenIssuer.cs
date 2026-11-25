using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PlatformService.Application.Abstractions;

namespace PlatformService.Infrastructure.Tokens;

/// <summary>
/// Signs the access tokens of ADR-4 with ES256, so that the gateway accepts them with its JWKS (PS-ADR-12).
/// </summary>
internal sealed class JwtAccessTokenIssuer : IAccessTokenIssuer, IDisposable
{
    /// <summary>The issuer and the audience of ADR-4, which the configuration of the gateway fixes too.</summary>
    public const string Issuer = "platform-service";

    public const string Audience = "chess-platform";

    /// <summary>The standard OpenID Connect claim of the short user name, which the gateway puts into x-user-name.</summary>
    public const string UserNameClaim = "preferred_username";

    private readonly ECDsa _key;
    private readonly SigningCredentials _signingCredentials;

    // Without the default times the handler writes no nbf, only the iat and the exp of the descriptor.
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };

    public JwtAccessTokenIssuer(IOptions<JwtOptions> options)
    {
        _key = SigningKeyFile.Load(options.Value.SigningKeyPath);

        // The kid is the JWK thumbprint of RFC 7638, which the script of the infra repo writes into the JWKS too.
        var securityKey = new ECDsaSecurityKey(_key);
        securityKey.KeyId = Base64Url.EncodeToString(securityKey.ComputeJwkThumbprint());

        // The default factory caches the signature providers in a static cache by the thumbprint of the key. Another
        // issuer with the same key, such as the one of another app in the tests, would then sign with this disposed key.
        _signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256)
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false },
        };
    }

    /// <summary>The lifetime of an access token (ADR-4).</summary>
    public static TimeSpan Lifetime { get; } = TimeSpan.FromMinutes(15);

    public IssuedToken Issue(UserAccount account, DateTimeOffset issuedAt)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = issuedAt.UtcDateTime,
            Expires = (issuedAt + Lifetime).UtcDateTime,
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [JwtRegisteredClaimNames.Sub] = account.Id.ToString(),
                [UserNameClaim] = account.UserName,
            },
            SigningCredentials = _signingCredentials,
        };

        return new IssuedToken(_handler.CreateToken(descriptor), Lifetime);
    }

    public void Dispose() => _key.Dispose();
}
