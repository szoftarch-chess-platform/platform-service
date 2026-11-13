namespace PlatformService.Api.Authentication;

/// <summary>
/// The identity contract between the Envoy gateway and the service, as decided in ADR-4.
/// </summary>
internal static class GatewayAuthenticationDefaults
{
    public const string AuthenticationScheme = "Gateway";

    /// <summary>The user's UUID v7 from the JWT <c>sub</c> claim. Envoy sets it with <c>claim_to_headers</c>.</summary>
    public const string UserIdHeader = "x-user-id";

    /// <summary>The user name from the JWT. Envoy sets it with <c>claim_to_headers</c>.</summary>
    public const string UserNameHeader = "x-user-name";

    public const string SubjectClaimType = "sub";

    public const string NameClaimType = "name";
}
