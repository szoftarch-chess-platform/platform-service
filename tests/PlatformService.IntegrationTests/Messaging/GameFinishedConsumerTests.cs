using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PlatformService.Application.Abstractions;
using PlatformService.Domain.Games;
using PlatformService.Infrastructure.Messaging;
using RabbitMQ.Client;

namespace PlatformService.IntegrationTests.Messaging;

/// <summary>
/// The consumer of PS-ADR-14 on a real RabbitMQ with the permissions of ADR-6 and the policy of the infra repo, against
/// the real database. Every test has a virtual host and an app of its own.
/// </summary>
public sealed class GameFinishedConsumerTests(PlatformApiFactory factory, RabbitMqFixture rabbitMq) : IClassFixture<PlatformApiFactory>
{
    [Fact]
    public async Task TheExamplesOfTheContract_ReachTheHistory()
    {
        await using var broker = await rabbitMq.CreateBrokerAsync();
        await broker.DeclareGameEventsAsync();
        await using var app = StartConsumer(broker);

        await broker.PublishAsync(GameEvents.WhiteWonByCheckmate, "0193c2c0-1a2b-7c3d-8e4f-5a6b7c8d9e0f");
        await broker.PublishAsync(GameEvents.BotGameResigned, "0193c2d1-2b3c-7d4e-9f5a-6b7c8d9e0f1a");

        var checkmate = await WaitForGameAsync(Guid.Parse("0193c2bf-0a1b-7c2d-9e3f-4a5b6c7d8e9f"));
        Assert.Equal(("whiteWon", "checkmate"), (checkmate.Result, checkmate.Reason));
        Assert.Equal(["e2e4", "e7e5", "d1h5", "b8c6", "f1c4", "g8f6", "h5f7"], checkmate.Moves);

        var resigned = await WaitForGameAsync(Guid.Parse("0193c2cf-3c4d-7e5f-8a6b-7c8d9e0f1a2b"));
        Assert.Equal(Player.Create("bot", null, "Stockfish bot"), resigned.White);
        Assert.Equal(Player.Create("user", GameEvents.Alice, "alice"), resigned.Black);
    }

    /// <summary>The consumer handles one message at a time in order, so it has answered the repeated one before the last.</summary>
    [Fact]
    public async Task ARepeatedEvent_IsAcknowledgedWithoutASecondGame()
    {
        await using var broker = await rabbitMq.CreateBrokerAsync();
        await broker.DeclareGameEventsAsync();
        await using var app = StartConsumer(broker);
        var user = Guid.CreateVersion7();
        var game = GameEvents.NewGame(user);
        var last = GameEvents.NewGame(user);

        await broker.PublishAsync(game);
        await broker.PublishAsync(game);
        await broker.PublishAsync(last);
        await WaitForGameAsync(last.GameId);

        Assert.Equal(0u, await broker.ReadyMessagesAsync(GameFinishedTopology.Queue));
        Assert.Equal(0u, await broker.ReadyMessagesAsync(GameFinishedTopology.DeadLetterQueue));
        Assert.Equal(2, await CountGamesAsync(user));
    }

    [Fact]
    public async Task ABodyThatIsNotJson_GoesToTheDeadLetterQueueAtOnce()
    {
        await using var broker = await rabbitMq.CreateBrokerAsync();
        await broker.DeclareGameEventsAsync();
        await using var app = StartConsumer(broker);

        await broker.PublishAsync("not json");

        var deadLetter = await broker.NextDeadLetterAsync();
        Assert.Equal("not json", Encoding.UTF8.GetString(deadLetter.Body.Span));
        Assert.Equal("rejected", DeathReason(deadLetter));
    }

    [Fact]
    public async Task AnEventOfAnotherType_GoesToTheDeadLetterQueueAtOnce()
    {
        await using var broker = await rabbitMq.CreateBrokerAsync();
        await broker.DeclareGameEventsAsync();
        await using var app = StartConsumer(broker);
        var game = GameEvents.NewGame(Guid.CreateVersion7(), json => json["type"] = "game.finished.v2");

        await broker.PublishAsync(game);

        Assert.Equal("rejected", DeathReason(await broker.NextDeadLetterAsync()));
        Assert.Null(await FindGameAsync(game.GameId));
    }

    [Fact]
    public async Task AGameThatBreaksTheContract_GoesToTheDeadLetterQueueAtOnce()
    {
        await using var broker = await rabbitMq.CreateBrokerAsync();
        await broker.DeclareGameEventsAsync();
        await using var app = StartConsumer(broker);
        var game = GameEvents.NewGame(Guid.CreateVersion7(), json => json["data"]!["moves"] = new JsonArray("e2e9"));

        await broker.PublishAsync(game);

        Assert.Equal("rejected", DeathReason(await broker.NextDeadLetterAsync()));
        Assert.Null(await FindGameAsync(game.GameId));
    }

    /// <summary>The platform-service may start before the game-service has declared its exchange (ADR-6).</summary>
    [Fact]
    public async Task TheBinding_WaitsUntilTheExchangeExists()
    {
        await using var broker = await rabbitMq.CreateBrokerAsync();
        await using var app = StartConsumer(broker);
        var game = GameEvents.NewGame(Guid.CreateVersion7());

        // The consumer has declared its queue by then, and the broker has refused its binding a few times.
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        await broker.DeclareGameEventsAsync();
        await broker.PublishAsync(game);

        await WaitForGameAsync(game.GameId);
    }

    /// <summary>
    /// A <c>basic.nack</c> does not count toward the delivery limit (PS-ADR-14), so the message fails once more than the
    /// limit allows a <c>basic.reject</c>, and the game is still stored.
    /// </summary>
    [Fact]
    public async Task ATransientError_DoesNotCountTowardTheDeliveryLimit()
    {
        await using var broker = await rabbitMq.CreateBrokerAsync();
        await broker.DeclareGameEventsAsync();
        const int failures = RabbitMqFixture.DeliveryLimit + 1;
        var failing = new FailingUseCase(call => call <= failures ? new TransientDbException() : null);
        await using var app = StartConsumer(broker, failing);
        var game = GameEvents.NewGame(Guid.CreateVersion7());

        await broker.PublishAsync(game);

        await WaitForGameAsync(game.GameId);
        Assert.Equal(failures + 1, failing.Calls);
        Assert.Equal(0u, await broker.ReadyMessagesAsync(GameFinishedTopology.DeadLetterQueue));
    }

    /// <summary>
    /// The message is delivered once more than the delivery limit of the policy, and that failure sends it to the
    /// dead-letter queue.
    /// </summary>
    [Fact]
    public async Task AnErrorThatRepeats_GoesToTheDeadLetterQueueAfterTheDeliveryLimit()
    {
        await using var broker = await rabbitMq.CreateBrokerAsync();
        await broker.DeclareGameEventsAsync();
        var failing = new FailingUseCase(_ => new InvalidOperationException("A failure that does not go away."));
        await using var app = StartConsumer(broker, failing);
        var game = GameEvents.NewGame(Guid.CreateVersion7());

        await broker.PublishAsync(game);

        var deadLetter = await broker.NextDeadLetterAsync();
        Assert.Equal("delivery_limit", DeathReason(deadLetter));
        Assert.Equal(game.EventId.ToString(), deadLetter.BasicProperties.MessageId);
        Assert.Equal(RabbitMqFixture.DeliveryLimit + 1, failing.Calls);
    }

    [Fact]
    public async Task TheConsumer_ConnectsAgainWhenTheBrokerClosesTheConnection()
    {
        await using var broker = await rabbitMq.CreateBrokerAsync();
        await broker.DeclareGameEventsAsync();
        await using var app = StartConsumer(broker);
        var game = GameEvents.NewGame(Guid.CreateVersion7());

        await broker.CloseServiceConnectionsAsync();
        await broker.PublishAsync(game);

        await WaitForGameAsync(game.GameId);
    }

    /// <summary>The accounts and the history work while the broker is down, and the games wait in the queue (PS-ADR-14).</summary>
    [Fact]
    public async Task Ready_WithoutTheBroker_Returns200()
    {
        await using var app = factory.WithConsumer("amqp://localhost:1/%2f");
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Starts the app with the consumer on the virtual host of the test.</summary>
    private WebApplicationFactory<Program> StartConsumer(TestBroker broker, FailingUseCase? failing = null)
    {
        var app = factory.WithConsumer(broker.ServiceUri, failing is null ? null : failing.Register);
        app.StartServer();
        return app;
    }

    private async Task<Game?> FindGameAsync(Guid gameId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IGameRepository>()
            .FindAsync(gameId, TestContext.Current.CancellationToken);
    }

    private Task<Game> WaitForGameAsync(Guid gameId) => Eventually.GetAsync(() => FindGameAsync(gameId), $"the game {gameId}");

    private async Task<int> CountGamesAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IGameRepository>()
            .CountByPlayerAsync(userId, TestContext.Current.CancellationToken);
    }

    /// <summary>Why RabbitMQ dead-lettered the message, which is <c>rejected</c> or <c>delivery_limit</c>.</summary>
    private static string DeathReason(BasicGetResult deadLetter) =>
        Encoding.UTF8.GetString((byte[])deadLetter.BasicProperties.Headers!["x-first-death-reason"]!);
}
