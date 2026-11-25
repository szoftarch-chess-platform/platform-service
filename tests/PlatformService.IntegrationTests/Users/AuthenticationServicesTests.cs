using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace PlatformService.IntegrationTests.Users;

/// <summary>The services of the Gateway scheme of PS-ADR-4.</summary>
public sealed class AuthenticationServicesTests(PlatformApiFactory factory) : IClassFixture<PlatformApiFactory>
{
    [Fact]
    public void App_RegistersNoDataProtection()
    {
        // Nothing in the service protects data, so its key ring would only start with warnings (PS-ADR-4).
        Assert.Null(factory.Services.GetService<IDataProtectionProvider>());
    }
}
