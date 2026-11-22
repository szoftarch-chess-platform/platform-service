using System.Security.Cryptography;

namespace PlatformService.Infrastructure.Tokens;

/// <summary>Reads the ES256 key of ADR-4, a P-256 private key in a PEM file.</summary>
internal static class SigningKeyFile
{
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The service may not read the file.</exception>
    /// <exception cref="ArgumentException">The file holds no PEM key.</exception>
    /// <exception cref="CryptographicException">The key is broken or not on the P-256 curve.</exception>
    public static ECDsa Load(string path)
    {
        var pem = File.ReadAllText(path);
        var key = ECDsa.Create();
        try
        {
            key.ImportFromPem(pem);
            var curve = key.ExportParameters(includePrivateParameters: false).Curve;
            if (!curve.IsNamed || curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value)
            {
                throw new CryptographicException("The key is not on the P-256 curve, which ES256 needs.");
            }

            return key;
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }
}
