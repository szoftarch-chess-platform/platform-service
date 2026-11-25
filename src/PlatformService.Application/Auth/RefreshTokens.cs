using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace PlatformService.Application.Auth;

/// <summary>The opaque refresh tokens of PS-ADR-12, which the database knows only by their hash.</summary>
internal static class RefreshTokens
{
    private const int SizeInBytes = 32;

    /// <summary>Creates a new token from random bytes in base64url, together with its hash.</summary>
    public static (string Value, byte[] Hash) Create()
    {
        var value = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SizeInBytes));
        return (value, Hash(value));
    }

    /// <summary>The SHA-256 hash that the database stores instead of the token.</summary>
    public static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
