using System.Security.Claims;
using PlatformService.Application.Abstractions;

namespace PlatformService.Api.Authentication;

/// <summary>
/// <see cref="ICurrentUser"/> backed by the principal that <see cref="GatewayAuthenticationHandler"/> created.
/// </summary>
internal sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId => Guid.Parse(GetRequiredClaim(ClaimTypes.NameIdentifier));

    public string UserName => GetRequiredClaim(GatewayAuthenticationDefaults.NameClaimType);

    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    private string GetRequiredClaim(string claimType)
    {
        if (!IsAuthenticated)
        {
            throw new InvalidOperationException("The current request has no authenticated user.");
        }

        return Principal!.FindFirst(claimType)?.Value
            ?? throw new InvalidOperationException($"The authenticated user has no '{claimType}' claim.");
    }
}
