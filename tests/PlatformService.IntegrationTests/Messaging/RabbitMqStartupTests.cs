using Microsoft.AspNetCore.Hosting;

namespace PlatformService.IntegrationTests.Messaging;

/// <summary>Without an AMQP URI the service stops at startup with an error that names the setting (PS-ADR-14).</summary>
public sealed class RabbitMqStartupTests(PlatformApiFactory factory) : IClassFixture<PlatformApiFactory>
{
    [Fact]
    public void Startup_WithoutTheSetting_Fails()
    {
        var message = StartupError(string.Empty);

        Assert.Contains("'ConnectionStrings:RabbitMq' is not set", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://rabbitmq:15672")]
    [InlineData("rabbitmq:5672")]
    [InlineData("Host=rabbitmq;Username=platform-service")]
    public void Startup_WithoutAnAmqpUri_Fails(string connectionString)
    {
        var message = StartupError(connectionString);

        Assert.Contains("'ConnectionStrings:RabbitMq' has to be an AMQP URI", message, StringComparison.Ordinal);
    }

    /// <summary>Starts the app with another connection string and returns the messages of the error that stopped it.</summary>
    private string StartupError(string connectionString)
    {
        using var app = factory.WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:RabbitMq", connectionString));

        var exception = Assert.ThrowsAny<Exception>(() => app.CreateClient().Dispose());

        return exception.ToString();
    }
}
