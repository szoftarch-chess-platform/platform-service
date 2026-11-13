using System.Net;

namespace PlatformService.IntegrationTests;

public sealed class HealthEndpointTests(PlatformApiFactory factory) : IClassFixture<PlatformApiFactory>
{
    [Fact]
    public async Task Live_WithoutIdentityHeaders_Returns200()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
