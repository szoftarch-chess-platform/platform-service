using System.Security.Cryptography;

namespace PlatformService.Infrastructure.Tokens;

/// <summary>Reads the ES256 key of ADR-4, a P-256 private key in a PEM file.</summary>
internal static class SigningKeyFile
{
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The service may not read the file.</exception>
    /// <exception cref="ArgumentException">The file holds no PEM key.</exception>
    /// <exception cref="CryptographicException">The key is broken, has no private part or is not on the P-256 curve.</exception>
    public static ECDsa Load(string path)
    {
        var pem = File.ReadAllText(path);
        var key = ECDsa.Create();
        try
        {
            key.ImportFromPem(pem);
            CheckCanSignEs256(key);
            return key;
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    // ImportFromPem takes a public key too, which cannot sign. Only the export of the private part tells them apart.
    private static void CheckCanSignEs256(ECDsa key)
    {
        ECParameters parameters;
        try
        {
            parameters = key.ExportParameters(includePrivateParameters: true);
        }
        catch (CryptographicException exception)
        {
            throw new CryptographicException("The file holds only a public key, but signing needs the private key.", exception);
        }

        // The check needs only the curve, so the copy of the private key does not stay in memory.
        CryptographicOperations.ZeroMemory(parameters.D);

        if (!parameters.Curve.IsNamed || parameters.Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value)
        {
            throw new CryptographicException("The key is not on the P-256 curve, which ES256 needs.");
        }
    }
}
