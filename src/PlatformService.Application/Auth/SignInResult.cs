using PlatformService.Application.Abstractions;
using PlatformService.Application.Users;

namespace PlatformService.Application.Auth;

/// <summary>The signed-in user with the two tokens, which the Api puts into the cookies of ADR-4.</summary>
public sealed record SignInResult(CurrentUserDto User, IssuedToken AccessToken, IssuedToken RefreshToken);
