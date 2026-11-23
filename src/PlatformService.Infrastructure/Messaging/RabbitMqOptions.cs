namespace PlatformService.Infrastructure.Messaging;

/// <summary>The connection to RabbitMQ, read from <c>ConnectionStrings:RabbitMq</c> (PS-ADR-14).</summary>
internal sealed class RabbitMqOptions
{
    public const string ConnectionStringName = "RabbitMq";

    /// <summary>The AMQP URI with the user of the service. It holds the password, so it is never logged.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// The wait before the consumer connects again or tries the binding again. It is not read from the configuration,
    /// and only the tests shorten it.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Whether the value is an absolute URI with the scheme of AMQP or of AMQP over TLS.</summary>
    public static bool IsAmqpUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "amqp" or "amqps";
}
