using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlatformService.Api.Authentication;
using PlatformService.Application.Auth;

namespace PlatformService.Api.Controllers;

/// <summary>
/// Refresh and sign-out, which come with the refresh token in its cookie and without an access token
/// (PS-ADR-2, PS-ADR-12). They share the path and the group of <see cref="AuthController"/>, as in the contract.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/auth")]
[Tags("Auth")]
public sealed class SessionsController(ISessionService sessionService) : ControllerBase
{
    /// <summary>Replaces the refresh token in the cookie with a new one and issues a new access token.</summary>
    /// <remarks>
    /// The 401 comes from the result and not from an exception, because the exception handler clears the headers of
    /// the response, and with them the deletion of the cookies (PS-ADR-12).
    /// </remarks>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var result = await sessionService.RefreshAsync(Request.Cookies[TokenCookies.RefreshTokenName], cancellationToken);
        if (!result.Succeeded)
        {
            TokenCookies.Delete(Response);
            return Problem(detail: RefreshResult.SessionEndedMessage, statusCode: StatusCodes.Status401Unauthorized);
        }

        TokenCookies.Append(Response, result.AccessToken, result.RefreshToken);
        return NoContent();
    }

    /// <summary>Revokes the session of the refresh token and deletes both cookies, also without a valid token.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await sessionService.LogoutAsync(Request.Cookies[TokenCookies.RefreshTokenName], cancellationToken);
        TokenCookies.Delete(Response);
        return NoContent();
    }
}
