namespace PlatformService.Application.Users;

/// <summary>Use cases about users.</summary>
public interface IUserService
{
    /// <summary>Returns the user who sent the current request.</summary>
    Task<CurrentUserDto> GetCurrentAsync(CancellationToken cancellationToken);
}
