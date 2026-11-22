namespace PlatformService.Application.Auth;

/// <summary>The body of <c>POST /api/auth/login</c>.</summary>
/// <remarks>A class rather than a record, so that <see cref="object.ToString"/> does not print the password.</remarks>
public sealed class LoginRequest
{
    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
