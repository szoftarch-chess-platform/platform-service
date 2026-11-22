using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace PlatformService.IntegrationTests.OpenApi;

/// <summary>The OpenAPI document of Development, which Scalar uses (PS-ADR-2).</summary>
public sealed class OpenApiDocumentTests(PlatformApiFactory factory) : IClassFixture<PlatformApiFactory>
{
    [Theory]
    [InlineData("/api/auth/register")]
    [InlineData("/api/auth/login")]
    public async Task AnonymousOperations_NeedNoIdentityHeaders(string path)
    {
        var document = await ReadDocumentAsync();

        var security = document.GetProperty("paths").GetProperty(path).GetProperty("post").GetProperty("security");

        Assert.Equal(0, security.GetArrayLength());
    }

    [Fact]
    public async Task OtherOperations_KeepTheIdentityHeadersOfTheDocument()
    {
        var document = await ReadDocumentAsync();

        var operation = document.GetProperty("paths").GetProperty("/api/users/me").GetProperty("get");

        Assert.False(operation.TryGetProperty("security", out _));
        Assert.Equal(1, document.GetProperty("security").GetArrayLength());
    }

    private async Task<System.Text.Json.JsonElement> ReadDocumentAsync()
    {
        using var app = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }
}
