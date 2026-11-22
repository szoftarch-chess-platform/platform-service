namespace PlatformService.Application.Abstractions;

/// <summary>Signs the access tokens of ADR-4, which the gateway checks (PS-ADR-12).</summary>
public interface IAccessTokenIssuer
{
    /// <summary>Signs an access token for the account, valid from <paramref name="issuedAt"/> for the lifetime of ADR-4.</summary>
    IssuedToken Issue(UserAccount account, DateTimeOffset issuedAt);
}
