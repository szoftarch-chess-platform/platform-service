using PlatformService.Application.Abstractions;

namespace PlatformService.Application.Users;

internal sealed class UserService(ICurrentUser currentUser) : IUserService
{
    public Task<CurrentUserDto> GetCurrentAsync(CancellationToken cancellationToken)
    {
        // The identity comes from the gateway's headers, so no database read is needed yet.
        var user = new CurrentUserDto(currentUser.UserId, currentUser.UserName);
        return Task.FromResult(user);
    }
}
