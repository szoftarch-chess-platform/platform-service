namespace PlatformService.Application.Users;

/// <summary>The signed-in user, as returned by <c>GET /api/users/me</c>.</summary>
public sealed record CurrentUserDto(Guid Id, string UserName);
