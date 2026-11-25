using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using PlatformService.Api.Authentication;
using PlatformService.Api.Correlation;
using PlatformService.Api.ErrorHandling;
using PlatformService.Api.OpenApi;
using PlatformService.Application.Abstractions;

namespace PlatformService.Api;

public static class DependencyInjection
{
    private const string KnownNetworksKey = "ForwardedHeaders:KnownNetworks";

    /// <summary>
    /// Registers the presentation layer. This covers forwarded headers, ProblemDetails, gateway authentication,
    /// the fallback authorization policy, controllers, JSON and OpenAPI.
    /// </summary>
    public static IServiceCollection AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddGatewayForwardedHeaders(configuration);

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["correlationId"] = RequestCorrelation.GetCorrelationId(context.HttpContext);
            context.ProblemDetails.Extensions.TryAdd(
                "traceId",
                Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
        });
        services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

        // AddAuthentication would also register Data Protection for the cookie and remote schemes, which this service
        // does not have, and its key ring would start with warnings about keys that nothing protects. So only the
        // core services and the encoders that the handler takes are registered.
        services.AddAuthenticationCore(options => options.DefaultScheme = GatewayAuthenticationDefaults.AuthenticationScheme);
        services.AddWebEncoders();
        new AuthenticationBuilder(services)
            .AddScheme<AuthenticationSchemeOptions, GatewayAuthenticationHandler>(
                GatewayAuthenticationDefaults.AuthenticationScheme,
                configureOptions: null);

        // Like Envoy on /api/*, every endpoint requires a user unless it is marked with AllowAnonymous.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        // Content rules live in FluentValidation validators. The automatic 400 response of ApiController
        // only covers binding and format errors, such as malformed JSON or a wrong type.
        services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
            .AddJsonOptions(options => ApplyJsonConventions(options.JsonSerializerOptions));

        // Used outside MVC, for example for the ProblemDetails of the exception handler and the status code pages.
        services.ConfigureHttpJsonOptions(options => ApplyJsonConventions(options.SerializerOptions));

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<GatewayIdentitySecuritySchemeTransformer>();
            options.AddOperationTransformer<AllowAnonymousSecurityTransformer>();
        });

        return services;
    }

    /// <summary>
    /// The JSON conventions of ADR-8. Enums are camelCase strings and are never read from numbers,
    /// and a property without a value is left out instead of being written as null.
    /// </summary>
    private static void ApplyJsonConventions(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    }

    /// <summary>
    /// TLS ends at the gateway, so the client IP and scheme come from X-Forwarded-For and X-Forwarded-Proto.
    /// These headers are trusted only from the configured networks.
    /// </summary>
    private static void AddGatewayForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ForwardedHeadersOptions>()
            .Configure(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                // The configured list replaces the loopback defaults.
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();
                foreach (var cidr in configuration.GetSection(KnownNetworksKey).Get<string[]>() ?? [])
                {
                    if (!System.Net.IPNetwork.TryParse(cidr, out var network))
                    {
                        throw new InvalidOperationException($"'{cidr}' in {KnownNetworksKey} is not a valid CIDR range.");
                    }

                    options.KnownIPNetworks.Add(network);
                }
            })
            .Validate(
                options => options.KnownIPNetworks.Count > 0,
                $"{KnownNetworksKey} must list at least one CIDR range, the network that the gateway connects from.")
            .ValidateOnStart();
    }
}
