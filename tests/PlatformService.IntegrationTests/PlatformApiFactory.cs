using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PlatformService.IntegrationTests.ErrorHandling;

namespace PlatformService.IntegrationTests;

/// <summary>
/// Hosts the real app in memory, plus a test-only controller that throws the mapped exceptions.
/// </summary>
public sealed class PlatformApiFactory : WebApplicationFactory<Program>
{
    // The app only connects for /health/ready, which these tests do not call.
    private const string TestConnectionString =
        "Host=localhost;Port=5432;Database=platform_test;Username=platform_test;Password=platform_test";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Testing is not Development, so errors look as in production and no user secrets are loaded.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Platform", TestConnectionString);

        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(ErrorTestController).Assembly);
            services.AddValidatorsFromAssemblyContaining<ErrorTestController>();
        });
    }
}
