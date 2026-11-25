using System.Diagnostics.CodeAnalysis;
using PlatformService.Application.Abstractions;

namespace PlatformService.Application.Auth;

/// <summary>
/// The new tokens of a refresh, or the end of the session. It is a result rather than an exception, because the Api
/// deletes both cookies with the 401, and the exception handler would clear them (PS-ADR-12).
/// </summary>
public sealed class RefreshResult
{
    /// <summary>The same for every reason, so the response tells nothing about the state of the token.</summary>
    public const string SessionEndedMessage = "The session has ended. Sign in again.";

    private RefreshResult(IssuedToken? accessToken, IssuedToken? refreshToken)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
    }

    /// <summary>The token is missing, unknown, expired or revoked, or it was replaced and came back after its grace window.</summary>
    public static RefreshResult SessionEnded { get; } = new(null, null);

    [MemberNotNullWhen(true, nameof(AccessToken), nameof(RefreshToken))]
    public bool Succeeded => AccessToken is not null;

    public IssuedToken? AccessToken { get; }

    public IssuedToken? RefreshToken { get; }

    public static RefreshResult Refreshed(IssuedToken accessToken, IssuedToken refreshToken) =>
        new(accessToken, refreshToken);
}
