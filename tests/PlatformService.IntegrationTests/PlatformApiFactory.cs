using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PlatformService.IntegrationTests.ErrorHandling;

namespace PlatformService.IntegrationTests;

/// <summary>
/// Hosts the real app in memory against the database of <see cref="PostgresFixture"/>,
/// plus the test-only controllers of this project.
/// </summary>
public sealed class PlatformApiFactory(PostgresFixture postgres) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Testing is not Development, so errors look as in production and no user secrets are loaded.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Platform", postgres.ConnectionString);

        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(ErrorTestController).Assembly);
            services.AddValidatorsFromAssemblyContaining<ErrorTestController>();
        });
    }
}
