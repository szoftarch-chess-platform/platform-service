using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PlatformService.Api.Authentication;

/// <summary>
/// Builds the user from the identity headers that Envoy sets after validating the JWT.
/// </summary>
/// <remarks>
/// Only <c>x-user-id</c> and <c>x-user-name</c> are read. Envoy strips just the headers listed in its
/// <c>claim_to_headers</c> from incoming requests, so any other <c>x-user-*</c> header could come from the client.
/// </remarks>
internal sealed partial class GatewayAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(GatewayAuthenticationDefaults.UserIdHeader, out var userIdValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // The user id is a UUID v7 according to ADR-4, but only the GUID format is checked here.
        if (userIdValues.Count != 1 || !Guid.TryParse(userIdValues[0], out var userId))
        {
            LogInvalidUserId(Logger, GatewayAuthenticationDefaults.UserIdHeader);
            return Task.FromResult(AuthenticateResult.Fail(
                $"The {GatewayAuthenticationDefaults.UserIdHeader} header is not a single valid GUID."));
        }

        var userNameValues = Request.Headers[GatewayAuthenticationDefaults.UserNameHeader];
        if (userNameValues.Count != 1 || string.IsNullOrWhiteSpace(userNameValues[0]))
        {
            LogMissingUserName(Logger, GatewayAuthenticationDefaults.UserNameHeader);
            return Task.FromResult(AuthenticateResult.Fail(
                $"The {GatewayAuthenticationDefaults.UserNameHeader} header is missing or empty."));
        }

        var id = userId.ToString();
        Claim[] claims =
        [
            new(GatewayAuthenticationDefaults.SubjectClaimType, id),
            new(ClaimTypes.NameIdentifier, id),
            new(GatewayAuthenticationDefaults.NameClaimType, userNameValues[0]!),
        ];
        var identity = new ClaimsIdentity(
            claims,
            Scheme.Name,
            nameType: GatewayAuthenticationDefaults.NameClaimType,
            roleType: ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected the request because the {Header} header is not a single valid GUID.")]
    private static partial void LogInvalidUserId(ILogger logger, string header);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected the request because the {Header} header is missing or empty.")]
    private static partial void LogMissingUserName(ILogger logger, string header);
}
