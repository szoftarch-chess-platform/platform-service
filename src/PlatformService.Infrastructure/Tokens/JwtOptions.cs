namespace PlatformService.Infrastructure.Tokens;

/// <summary>The settings of the access tokens, read from the <c>Jwt</c> section.</summary>
internal sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>The PEM file with the private key of ADR-4, which compose mounts into the container.</summary>
    public string SigningKeyPath { get; set; } = string.Empty;
}
