using System.Net;
using PlatformService.HealthProbe;

namespace PlatformService.UnitTests.Health;

public sealed class ProbeTests
{
    private static readonly Uri _url = new("http://127.0.0.1:8080/health/ready");

    [Fact]
    public void TryGetUrl_WithOneHttpUrl_ReturnsIt()
    {
        Assert.True(Probe.TryGetUrl([_url.OriginalString], out var url));
        Assert.Equal(_url, url);
    }

    [Theory]
    [InlineData("health/ready")]
    [InlineData("/health/ready")]
    [InlineData("ftp://127.0.0.1/health/ready")]
    public void TryGetUrl_WithAnArgumentThatIsNotAnHttpUrl_ReturnsFalse(string argument) =>
        Assert.False(Probe.TryGetUrl([argument], out _));

    [Fact]
    public void TryGetUrl_WithoutExactlyOneArgument_ReturnsFalse()
    {
        Assert.False(Probe.TryGetUrl([], out _));
        Assert.False(Probe.TryGetUrl([_url.OriginalString, _url.OriginalString], out _));
    }

    [Fact]
    public async Task CheckAsync_IsHealthyForASuccessStatusCode()
    {
        var error = new StringWriter();
        using var client = new HttpClient(new AnsweringHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

        var exitCode = await Probe.CheckAsync(client, _url, error, TestContext.Current.CancellationToken);

        Assert.Equal(Probe.Healthy, exitCode);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task CheckAsync_IsUnhealthyForAnErrorStatusCode()
    {
        var error = new StringWriter();
        using var client = new HttpClient(new AnsweringHandler(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));

        var exitCode = await Probe.CheckAsync(client, _url, error, TestContext.Current.CancellationToken);

        Assert.Equal(Probe.Unhealthy, exitCode);
        Assert.Contains("answered 503", error.ToString());
    }

    [Fact]
    public async Task CheckAsync_IsUnhealthyWhenTheServiceDoesNotAnswer()
    {
        var error = new StringWriter();
        using var client = new HttpClient(new AnsweringHandler(
            _ => throw new HttpRequestException("Connection refused")));

        var exitCode = await Probe.CheckAsync(client, _url, error, TestContext.Current.CancellationToken);

        Assert.Equal(Probe.Unhealthy, exitCode);
        Assert.Contains("Connection refused", error.ToString());
    }

    [Fact]
    public async Task CheckAsync_IsUnhealthyAfterTheTimeout()
    {
        var error = new StringWriter();
        using var client = new HttpClient(new AnsweringHandler(async cancellationToken =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }))
        {
            Timeout = TimeSpan.FromMilliseconds(50),
        };

        var exitCode = await Probe.CheckAsync(client, _url, error, TestContext.Current.CancellationToken);

        Assert.Equal(Probe.Unhealthy, exitCode);
        Assert.Contains("did not answer within", error.ToString());
    }

    /// <summary>Answers every request with the given function instead of the network.</summary>
    private sealed class AnsweringHandler(Func<CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => answer(cancellationToken);
    }
}
