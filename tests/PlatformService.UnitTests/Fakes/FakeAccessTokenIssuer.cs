using PlatformService.Application.Abstractions;

namespace PlatformService.UnitTests.Fakes;

/// <summary>Issues a readable token that shows for whom and when it was issued.</summary>
internal sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
{
    public static TimeSpan Lifetime { get; } = TimeSpan.FromMinutes(15);

    public IssuedToken Issue(UserAccount account, DateTimeOffset issuedAt) =>
        new(Token(account, issuedAt), Lifetime);

    public static string Token(UserAccount account, DateTimeOffset issuedAt) => $"access {account.Id} {issuedAt:O}";
}
