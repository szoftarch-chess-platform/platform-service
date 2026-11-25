using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using PlatformService.Api;
using PlatformService.Api.Correlation;
using PlatformService.Application;
using PlatformService.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Outside Development the console logs JSON with scopes and UTC timestamps.
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.UseUtcTimestamp = true;
        options.TimestampFormat = "O";
    });
}

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPresentation(builder.Configuration);

var app = builder.Build();

// The middleware below is the path of a request from the gateway to the database, in this order.

// The client IP and scheme come from X-Forwarded-For and X-Forwarded-Proto, trusted only from the gateway's network.
// TLS ends at the gateway, so there is no HTTPS redirection, HSTS, CORS or rate limiting here.
app.UseForwardedHeaders();

// Exceptions and error responses without a body, such as a 401 from authorization, become ProblemDetails.
app.UseExceptionHandler();
app.UseStatusCodePages();

// The gateway's x-request-id goes into the logging scope as CorrelationId and into every ProblemDetails.
app.UseRequestCorrelation();

// The Gateway scheme builds the user from the x-user-id and x-user-name headers.
app.UseAuthentication();

// The fallback policy requires an authenticated user on every endpoint that does not allow anonymous access.
app.UseAuthorization();

// The controller calls an Application service, which reaches the Infrastructure through its ports.
app.MapControllers();

// The health endpoints are not under /api, so the gateway does not expose them.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(HealthCheckTags.Ready) })
    .AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

await app.RunAsync();
