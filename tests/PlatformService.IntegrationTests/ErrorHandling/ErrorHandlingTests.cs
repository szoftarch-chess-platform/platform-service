using System.Net;
using System.Text;

namespace PlatformService.IntegrationTests.ErrorHandling;

public sealed class ErrorHandlingTests(PlatformApiFactory factory) : IClassFixture<PlatformApiFactory>
{
    [Theory]
    [InlineData("not-found", 404)]
    [InlineData("conflict", 409)]
    [InlineData("unauthorized", 401)]
    [InlineData("forbidden", 403)]
    [InlineData("validation", 400)]
    [InlineData("unhandled", 500)]
    public async Task Exception_BecomesProblemDetailsWithTheCorrelationId(string kind, int expectedStatus)
    {
        using var client = factory.CreateClient();
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/test/errors/{kind}");
        request.Headers.Add("x-request-id", correlationId);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadJsonAsync();
        Assert.Equal(expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.Equal(correlationId, problem.GetProperty("correlationId").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task ValidationException_GroupsErrorsByCamelCaseProperty()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/test/errors/validation", TestContext.Current.CancellationToken);

        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        Assert.Equal(["userName", "address.postalCode"], errors.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            ["User name is required.", "User name is too short."],
            errors.GetProperty("userName").EnumerateArray().Select(message => message.GetString()));
    }

    [Fact]
    public async Task ConflictException_WithoutFields_HasNoErrors()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/test/errors/conflict", TestContext.Current.CancellationToken);

        var problem = await response.ReadJsonAsync();
        Assert.Equal("The user name is already taken.", problem.GetProperty("detail").GetString());
        Assert.False(problem.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task UnhandledException_HidesTheDetails()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/test/errors/unhandled", TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(ErrorTestController.UnhandledMessage, body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);
        Assert.False((await response.ReadJsonAsync()).TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task Validator_RejectsInvalidInputWith400()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/test/errors/validate", content, TestContext.Current.CancellationToken);

        // The message comes from the FluentValidation validator and not from an implicit Required check of MVC.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        Assert.Equal(["userName"], errors.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            [TestRequestValidator.UserNameRequired],
            errors.GetProperty("userName").EnumerateArray().Select(message => message.GetString()));
    }

    [Fact]
    public async Task MalformedJson_Returns400ProblemDetailsWithTheCorrelationId()
    {
        using var client = factory.CreateClient();
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/test/errors/validate")
        {
            Content = new StringContent("{ not json", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-request-id", correlationId);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadJsonAsync();
        Assert.Equal(correlationId, problem.GetProperty("correlationId").GetString());
        Assert.True(problem.TryGetProperty("errors", out _));
    }
}
