using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace PlatformService.Infrastructure.Tokens;

/// <summary>
/// Stops the service at startup when the signing key is missing or broken, instead of failing the first sign-in
/// (PS-ADR-12).
/// </summary>
internal sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    private const string Setting = $"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKeyPath)}";

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKeyPath))
        {
            return ValidateOptionsResult.Fail(
                $"The setting '{Setting}' is not set. Set it with the {Setting.Replace(":", "__", StringComparison.Ordinal)} "
                + "environment variable or locally with dotnet user-secrets.");
        }

        try
        {
            using var key = SigningKeyFile.Load(options.SigningKeyPath);
            return ValidateOptionsResult.Success;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or CryptographicException)
        {
            return ValidateOptionsResult.Fail(
                $"The setting '{Setting}' has to name a PEM file with a P-256 private key, but '{options.SigningKeyPath}' "
                + $"could not be read as one. {exception.Message}");
        }
    }
}
