using PlatformService.Application.Users;
using PlatformService.UnitTests.Fakes;

namespace PlatformService.UnitTests.Users;

public sealed class UserServiceTests
{
    [Fact]
    public async Task GetCurrentAsync_ReturnsTheCurrentUser()
    {
        var userId = Guid.CreateVersion7();
        var service = new UserService(new FakeCurrentUser(userId, "alice"));

        var user = await service.GetCurrentAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new CurrentUserDto(userId, "alice"), user);
    }
}
