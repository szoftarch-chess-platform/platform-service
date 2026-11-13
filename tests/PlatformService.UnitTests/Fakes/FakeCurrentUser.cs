using PlatformService.Application.Abstractions;

namespace PlatformService.UnitTests.Fakes;

internal sealed class FakeCurrentUser(Guid userId, string userName) : ICurrentUser
{
    public bool IsAuthenticated => true;

    public Guid UserId { get; } = userId;

    public string UserName { get; } = userName;
}
