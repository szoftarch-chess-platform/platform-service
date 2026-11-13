using System.Net;

namespace PlatformService.IntegrationTests.Users;

public sealed class CurrentUserEndpointTests(PlatformApiFactory factory) : IClassFixture<PlatformApiFactory>
{
    private const string Path = "/api/users/me";

    [Fact]
    public async Task WithoutIdentityHeaders_Returns401ProblemDetails()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadJsonAsync();
        Assert.Equal(401, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("correlationId").GetString()));
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task WithIdentityHeaders_ReturnsTheUser()
    {
        using var client = factory.CreateClient();
        var userId = Guid.CreateVersion7();
        using var request = CreateRequest(userId.ToString(), "alice");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var user = await response.ReadJsonAsync();
        Assert.Equal(["id", "userName"], user.EnumerateObject().Select(property => property.Name));
        Assert.Equal(userId, user.GetProperty("id").GetGuid());
        Assert.Equal("alice", user.GetProperty("userName").GetString());
    }

    [Theory]
    [InlineData("not-a-guid", "alice")]
    [InlineData("0193c2b4-7d3e-7c2a-9f1e-3b5a6c7d8e9f", "")]
    [InlineData("0193c2b4-7d3e-7c2a-9f1e-3b5a6c7d8e9f", null)]
    public async Task WithInvalidIdentityHeaders_Returns401(string userId, string? userName)
    {
        using var client = factory.CreateClient();
        using var request = CreateRequest(userId, userName);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private static HttpRequestMessage CreateRequest(string userId, string? userName)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Path);
        request.Headers.TryAddWithoutValidation("x-user-id", userId);
        if (userName is not null)
        {
            request.Headers.TryAddWithoutValidation("x-user-name", userName);
        }

        return request;
    }
}
