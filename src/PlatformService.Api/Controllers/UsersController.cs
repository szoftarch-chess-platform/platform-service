using Microsoft.AspNetCore.Mvc;
using PlatformService.Application.Users;

namespace PlatformService.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IUserService userService) : ControllerBase
{
    /// <summary>
    /// The signed-in user. The access token is in an HttpOnly cookie, so this is how the frontend learns who is signed in.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<CurrentUserDto>> GetCurrentUser(CancellationToken cancellationToken) =>
        await userService.GetCurrentAsync(cancellationToken);
}
