using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using PlatformService.Api.Authentication;

namespace PlatformService.Api.OpenApi;

/// <summary>
/// Describes the gateway's identity headers as API key security schemes, so that Scalar can send
/// requests as a signed-in user. It is only used in Development, where there is no gateway.
/// </summary>
internal sealed class GatewayIdentitySecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private const string UserIdScheme = "UserId";
    private const string UserNameScheme = "UserName";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[UserIdScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = GatewayAuthenticationDefaults.UserIdHeader,
            Description = "The user's id as a GUID. The gateway sets it from the JWT `sub` claim.",
        };
        document.Components.SecuritySchemes[UserNameScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = GatewayAuthenticationDefaults.UserNameHeader,
            Description = "The user's user name. The gateway sets it from the JWT.",
        };

        // Both headers are required together, except where AllowAnonymousSecurityTransformer exempts an operation.
        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(UserIdScheme, document)] = [],
            [new OpenApiSecuritySchemeReference(UserNameScheme, document)] = [],
        });

        return Task.CompletedTask;
    }
}
