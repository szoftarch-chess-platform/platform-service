using System.Security.Cryptography;
using PlatformService.IntegrationTests;

[assembly: AssemblyFixture(typeof(SigningKeyFixture))]

namespace PlatformService.IntegrationTests;

/// <summary>
/// A P-256 key of ES256 in a temporary PEM file for the whole test assembly, because the service does not start
/// without one (PS-ADR-12).
/// </summary>
public sealed class SigningKeyFixture : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("platform-service-tests-");

    public SigningKeyFixture()
    {
        KeyPath = WriteKey(Key);
    }

    /// <summary>The key, whose public part checks the signatures of the access tokens.</summary>
    public ECDsa Key { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public string KeyPath { get; }

    /// <summary>Writes another key into a new file of the temporary folder, for example on a wrong curve.</summary>
    public string WriteKey(ECDsa key)
    {
        var path = Path.Combine(_directory.FullName, $"{Guid.NewGuid():N}.pem");
        File.WriteAllText(path, key.ExportPkcs8PrivateKeyPem());
        return path;
    }

    /// <summary>A path in the temporary folder where no file exists.</summary>
    public string MissingPath() => Path.Combine(_directory.FullName, $"{Guid.NewGuid():N}.pem");

    public void Dispose()
    {
        Key.Dispose();
        _directory.Delete(recursive: true);
    }
}
