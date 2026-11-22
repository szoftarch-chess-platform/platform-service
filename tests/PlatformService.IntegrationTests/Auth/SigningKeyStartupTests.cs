using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;

namespace PlatformService.IntegrationTests.Auth;

/// <summary>Without a valid signing key the service stops at startup with an error that names the setting.</summary>
public sealed class SigningKeyStartupTests(PlatformApiFactory factory, SigningKeyFixture signingKey) : IClassFixture<PlatformApiFactory>
{
    [Fact]
    public void Startup_WithoutTheSetting_Fails()
    {
        var message = StartupError(string.Empty);

        Assert.Contains("'Jwt:SigningKeyPath' is not set", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_WithAMissingFile_Fails()
    {
        var message = StartupError(signingKey.MissingPath());

        Assert.Contains("'Jwt:SigningKeyPath' has to name a PEM file with a P-256 private key", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_WithAKeyOnAnotherCurve_Fails()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP384);

        var message = StartupError(signingKey.WriteKey(key));

        Assert.Contains("not on the P-256 curve", message, StringComparison.Ordinal);
    }

    /// <summary>Starts the app with another key path and returns the messages of the error that stopped it.</summary>
    private string StartupError(string signingKeyPath)
    {
        using var app = factory.WithWebHostBuilder(builder => builder.UseSetting("Jwt:SigningKeyPath", signingKeyPath));

        var exception = Assert.ThrowsAny<Exception>(() => app.CreateClient().Dispose());

        return exception.ToString();
    }
}
