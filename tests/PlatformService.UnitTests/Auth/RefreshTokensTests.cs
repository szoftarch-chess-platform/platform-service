using System.Security.Cryptography;
using System.Text;
using PlatformService.Application.Auth;

namespace PlatformService.UnitTests.Auth;

public sealed class RefreshTokensTests
{
    [Fact]
    public void Create_Returns32RandomBytesInBase64Url()
    {
        var (value, _) = RefreshTokens.Create();

        // 32 bytes are 43 characters of base64url without padding.
        Assert.Matches("^[A-Za-z0-9_-]{43}$", value);
        Assert.NotEqual(value, RefreshTokens.Create().Value);
    }

    [Fact]
    public void Create_ReturnsTheSha256HashOfTheToken()
    {
        var (value, hash) = RefreshTokens.Create();

        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(value)), hash);
        Assert.Equal(hash, RefreshTokens.Hash(value));
    }
}
