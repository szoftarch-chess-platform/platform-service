using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PlatformService.Api.OpenApi;

/// <summary>
/// Exempts the operations that allow anonymous access, such as registration, from the identity headers that
/// <see cref="GatewayIdentitySecuritySchemeTransformer"/> requires for the whole document (PS-ADR-2).
/// </summary>
internal sealed class AllowAnonymousSecurityTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            // An empty list overrides the requirement of the document, which the contract writes as security: [].
            operation.Security = [];
        }

        return Task.CompletedTask;
    }
}
