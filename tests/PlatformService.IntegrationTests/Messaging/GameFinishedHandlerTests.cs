using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PlatformService.Application.Abstractions;
using PlatformService.Application.Games;
using PlatformService.Infrastructure;
using PlatformService.Infrastructure.Messaging;
using PlatformService.Infrastructure.Persistence;

namespace PlatformService.IntegrationTests.Messaging;

/// <summary>How the handler of PS-ADR-14 reads a message and which answer the broker gets, without a broker.</summary>
public sealed class GameFinishedHandlerTests(PlatformApiFactory factory) : IClassFixture<PlatformApiFactory>
{
    [Fact]
    public async Task HandleAsync_TheExampleAgainstTheBot_MapsEveryField()
    {
        RecordFinishedGameCommand? recorded = null;
        var handler = HandlerWith((command, _) =>
        {
            recorded = command;
            return Task.FromResult(RecordOutcome.Recorded);
        });

        Assert.Equal(DeliveryOutcome.Acknowledge, await HandleAsync(handler, GameEvents.BotGameResigned));

        Assert.NotNull(recorded);
        Assert.Equal(Guid.Parse("0193c2d1-2b3c-7d4e-9f5a-6b7c8d9e0f1a"), recorded.EventId);
        Assert.Equal(Guid.Parse("0193c2cf-3c4d-7e5f-8a6b-7c8d9e0f1a2b"), recorded.GameId);
        Assert.Equal(("standard", "whiteWon", "resignation"), (recorded.Variant, recorded.Result, recorded.Reason));
        Assert.Equal(new PlayerDto("bot", null, "Stockfish bot"), recorded.White);
        Assert.Equal(new PlayerDto("user", GameEvents.Alice, "alice"), recorded.Black);
        Assert.Equal(["e2e4", "c7c5"], recorded.Moves);
        Assert.Equal(new TimeControlDto(600000), recorded.TimeControl);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 15, 9, 30, TimeSpan.Zero), recorded.StartedAt);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 15, 10, 5, TimeSpan.Zero), recorded.EndedAt);
    }

    [Fact]
    public async Task HandleAsync_AGameStoredNowOrBefore_IsAcknowledged()
    {
        var handler = factory.Services.GetRequiredService<GameFinishedHandler>();
        var game = GameEvents.NewGame(Guid.CreateVersion7());

        Assert.Equal(DeliveryOutcome.Acknowledge, await HandleAsync(handler, game.Json));
        Assert.Equal(DeliveryOutcome.Acknowledge, await HandleAsync(handler, game.Json));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{ "id": "not a uuid", "type": "game.finished.v1" }""")]
    [InlineData("""{ "id": "0193c2c0-1a2b-7c3d-8e4f-5a6b7c8d9e0f", "type": "game.finished.v1", "data": { "timeControl": { "initialMs": 1.5 } } }""")]
    public async Task HandleAsync_ABodyThatIsNotAnEvent_GoesToTheDeadLetterQueue(string body)
    {
        var handler = HandlerWith((_, _) => throw new InvalidOperationException("The use case must not be called."));

        Assert.Equal(DeliveryOutcome.DeadLetter, await HandleAsync(handler, body));
    }

    [Theory]
    [InlineData("""{ "id": "0193c2c0-1a2b-7c3d-8e4f-5a6b7c8d9e0f", "type": "game.finished.v2", "data": {} }""")]
    [InlineData("""{ "id": "0193c2c0-1a2b-7c3d-8e4f-5a6b7c8d9e0f", "data": {} }""")]
    public async Task HandleAsync_AnotherType_GoesToTheDeadLetterQueue(string body)
    {
        var handler = HandlerWith((_, _) => throw new InvalidOperationException("The use case must not be called."));

        Assert.Equal(DeliveryOutcome.DeadLetter, await HandleAsync(handler, body));
    }

    /// <summary>The validator of PS-ADR-13 refuses every missing value, so an event without its game breaks the contract.</summary>
    [Theory]
    [InlineData("""{ "id": "0193c2c0-1a2b-7c3d-8e4f-5a6b7c8d9e0f", "type": "game.finished.v1" }""")]
    [InlineData("""{ "id": "0193c2c0-1a2b-7c3d-8e4f-5a6b7c8d9e0f", "type": "game.finished.v1", "data": null }""")]
    [InlineData("""{ "id": "0193c2c0-1a2b-7c3d-8e4f-5a6b7c8d9e0f", "type": "game.finished.v1", "data": { "white": null } }""")]
    public async Task HandleAsync_AGameThatBreaksTheContract_GoesToTheDeadLetterQueue(string body)
    {
        var handler = factory.Services.GetRequiredService<GameFinishedHandler>();

        Assert.Equal(DeliveryOutcome.DeadLetter, await HandleAsync(handler, body));
    }

    [Fact]
    public async Task HandleAsync_ATransientError_IsRetried()
    {
        var handler = HandlerWith((_, _) => throw new InvalidOperationException("Saving failed.", new TransientDbException()));

        Assert.Equal(DeliveryOutcome.Retry, await HandleAsync(handler, GameEvents.WhiteWonByCheckmate));
    }

    [Fact]
    public async Task HandleAsync_AnyOtherError_Fails()
    {
        var handler = HandlerWith((_, _) => throw new InvalidOperationException("A failure that does not go away."));

        Assert.Equal(DeliveryOutcome.Fail, await HandleAsync(handler, GameEvents.WhiteWonByCheckmate));
    }

    [Fact]
    public async Task HandleAsync_CancelledByTheStop_IsRetried()
    {
        using var stopping = new CancellationTokenSource();
        var handler = HandlerWith(async (_, cancellationToken) =>
        {
            await stopping.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return RecordOutcome.Recorded;
        });

        var outcome = await handler.HandleAsync(Encoding.UTF8.GetBytes(GameEvents.WhiteWonByCheckmate), null, stopping.Token);

        Assert.Equal(DeliveryOutcome.Retry, outcome);
    }

    [Fact]
    public void IsTransient_TimeoutExceptionInTheChain_IsTrue()
    {
        Assert.True(GameFinishedHandler.IsTransient(new InvalidOperationException("Saving failed.", new TimeoutException())));
    }

    [Fact]
    public void IsTransient_OtherExceptions_IsFalse()
    {
        Assert.False(GameFinishedHandler.IsTransient(new InvalidOperationException("A bug.", new ArgumentException("A bug."))));
    }

    /// <summary>Npgsql reports a database that it cannot reach as transient, which is what the handler relies on.</summary>
    [Fact]
    public async Task IsTransient_ADatabaseThatCannotBeReached_IsTrue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new(
                $"ConnectionStrings:{DatabaseOptions.ConnectionStringName}",
                "Host=localhost;Port=1;Database=platform;Username=platform;Password=none;Timeout=3")])
            .Build();
        await using var services = new ServiceCollection().AddLogging().AddInfrastructure(configuration).BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var games = scope.ServiceProvider.GetRequiredService<IGameRepository>();

        var exception = await Assert.ThrowsAnyAsync<Exception>(
            () => games.IsRecordedAsync(Guid.CreateVersion7(), Guid.CreateVersion7(), TestContext.Current.CancellationToken));

        Assert.True(GameFinishedHandler.IsTransient(exception), exception.ToString());
    }

    [Fact]
    public void AddInfrastructure_RunsTheConsumerAsAHostedService()
    {
        var services = new ServiceCollection().AddInfrastructure(new ConfigurationBuilder().Build());

        Assert.Contains(services, service =>
            service.ServiceType == typeof(IHostedService)
            && service.ImplementationType == typeof(GameFinishedConsumer));
    }

    /// <summary>A handler whose use case is the given function instead of the one of the app.</summary>
    private static GameFinishedHandler HandlerWith(Func<RecordFinishedGameCommand, CancellationToken, Task<RecordOutcome>> record)
    {
        var services = new ServiceCollection()
            .AddSingleton<IFinishedGameService>(new FunctionUseCase(record))
            .BuildServiceProvider();
        return new GameFinishedHandler(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<GameFinishedHandler>.Instance);
    }

    private static Task<DeliveryOutcome> HandleAsync(GameFinishedHandler handler, string body) =>
        handler.HandleAsync(Encoding.UTF8.GetBytes(body), "message-id", TestContext.Current.CancellationToken);

    private sealed class FunctionUseCase(Func<RecordFinishedGameCommand, CancellationToken, Task<RecordOutcome>> record)
        : IFinishedGameService
    {
        public Task<RecordOutcome> RecordAsync(RecordFinishedGameCommand command, CancellationToken cancellationToken) =>
            record(command, cancellationToken);
    }
}
