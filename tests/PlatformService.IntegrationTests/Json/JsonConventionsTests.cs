using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PlatformService.IntegrationTests.Json;

public sealed class JsonConventionsTests(PlatformApiFactory factory) : IClassFixture<PlatformApiFactory>
{
    private const string Path = "/api/test/json";

    [Fact]
    public async Task Response_WritesEnumsAsCamelCaseStringsWithoutNullValues()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal("""{"reason":"threefoldRepetition"}""", body);
    }

    [Fact]
    public async Task Request_ReadsEnumsFromCamelCaseStrings()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent("""{"reason":"threefoldRepetition"}""", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(Path, content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("threefoldRepetition", (await response.ReadJsonAsync()).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Request_WithAnEnumAsNumber_Returns400()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent("""{"reason":1}""", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(Path, content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>The exception handler and the status code pages write with these options instead of the ones of MVC.</summary>
    [Fact]
    public void MinimalApiOptions_FollowTheSameConventions()
    {
        var options = factory.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value;

        var json = JsonSerializer.Serialize(new JsonTestBody(JsonTestReason.ThreefoldRepetition, Note: null), options.SerializerOptions);

        Assert.Equal("""{"reason":"threefoldRepetition"}""", json);
    }
}
