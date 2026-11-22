using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformService.Api.Authentication;
using PlatformService.Application.Auth;
using PlatformService.Application.Users;

namespace PlatformService.Api.Controllers;

/// <summary>Registration and sign-in, which come without a token (PS-ADR-2, PS-ADR-12).</summary>
[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Creates the account and signs the user in, so the response sets both token cookies.</summary>
    /// <remarks>The 409 has the shape of a validation problem, because its <c>errors</c> name the taken fields (PS-ADR-5).</remarks>
    [HttpPost("register")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<CurrentUserDto>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        TokenCookies.Append(Response, result);
        return StatusCode(StatusCodes.Status201Created, result.User);
    }

    /// <summary>Signs the user in with the user name and the password, and sets both token cookies.</summary>
    [HttpPost("login")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<CurrentUserDto>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        TokenCookies.Append(Response, result);
        return result.User;
    }
}
