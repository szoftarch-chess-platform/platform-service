using System.Net;
using System.Text.Json;
using PlatformService.Application.Games;
using static PlatformService.IntegrationTests.Games.RecordedGames;

namespace PlatformService.IntegrationTests.Games;

/// <summary>The history endpoints of the contract on the games in PostgreSQL (PS-ADR-13).</summary>
public sealed class GameEndpointTests(PlatformApiFactory factory) : IClassFixture<PlatformApiFactory>
{
    private static readonly DateTimeOffset _end = new(2026, 11, 20, 14, 3, 12, 345, TimeSpan.Zero);

    [Fact]
    public async Task List_ReturnsTheGamesOfTheUserNewestFirstWithTheIdAsTheTieBreaker()
    {
        var user = Guid.CreateVersion7();
        var (lower, higher) = TwoIds();
        var first = await RecordNewAsync(User(user), User(Guid.CreateVersion7(), "bob"), _end);
        var againstTheBot = await RecordNewAsync(Bot, User(user), _end.AddMinutes(1));
        var asBlack = await RecordNewAsync(User(Guid.CreateVersion7(), "bob"), User(user), _end.AddMinutes(2));
        await RecordNewAsync(User(Guid.CreateVersion7(), "bob"), User(Guid.CreateVersion7(), "carol"), _end.AddMinutes(3));
        await RecordNewAsync(User(user), Bot, _end.AddMinutes(4), lower);
        await RecordNewAsync(User(user), Bot, _end.AddMinutes(4), higher);

        var page = await GetJsonAsync("/api/games", user);

        Assert.Equal(
            [higher, lower, asBlack, againstTheBot, first],
            page.GetProperty("items").EnumerateArray().Select(game => game.GetRequiredGuid("id")));
        Assert.Equal(1, page.GetProperty("page").GetInt32());
        Assert.Equal(20, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(5, page.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task List_CutsThePagesWithoutOverlap()
    {
        var user = Guid.CreateVersion7();
        var ids = new List<Guid>();
        for (var minutes = 0; minutes < 5; minutes++)
        {
            ids.Insert(0, await RecordNewAsync(User(user), Bot, _end.AddMinutes(minutes)));
        }

        var pages = new List<Guid>();
        foreach (var pageNumber in new[] { 1, 2, 3 })
        {
            var page = await GetJsonAsync($"/api/games?page={pageNumber}&pageSize=2", user);
            Assert.Equal(5, page.GetProperty("totalCount").GetInt32());
            pages.AddRange(page.GetProperty("items").EnumerateArray().Select(game => game.GetRequiredGuid("id")));
        }

        Assert.Equal(ids, pages);
    }

    [Fact]
    public async Task List_AfterTheLastPage_ReturnsNoGames()
    {
        var user = Guid.CreateVersion7();
        await RecordNewAsync(User(user), Bot, _end);

        var page = await GetJsonAsync("/api/games?page=2147483647&pageSize=100", user);

        Assert.Equal(0, page.GetProperty("items").GetArrayLength());
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task List_WithoutGames_ReturnsAnEmptyPage()
    {
        var page = await GetJsonAsync("/api/games", Guid.CreateVersion7());

        Assert.Equal(0, page.GetProperty("items").GetArrayLength());
        Assert.Equal(0, page.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task List_WritesTheSummaryOfTheContract()
    {
        var user = Guid.CreateVersion7();
        var gameId = await RecordNewAsync(Bot, User(user), _end);

        var page = await GetJsonAsync("/api/games", user);

        var game = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(
            ["id", "variant", "white", "black", "result", "reason", "startedAt", "endedAt"],
            game.EnumerateObject().Select(property => property.Name));
        Assert.Equal(gameId, game.GetRequiredGuid("id"));
        Assert.Equal("standard", game.GetRequiredString("variant"));
        Assert.Equal(("whiteWon", "checkmate"), (game.GetRequiredString("result"), game.GetRequiredString("reason")));
        AssertBot(game.GetProperty("white"));
        AssertUser(game.GetProperty("black"), user, "alice");
        Assert.Equal(_end.AddMinutes(-2), game.GetProperty("startedAt").GetDateTimeOffset());
        Assert.Equal(_end, game.GetProperty("endedAt").GetDateTimeOffset());
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("page=-1", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("page=first", "page")]
    [InlineData("pageSize=2.5", "pageSize")]
    public async Task List_WithAQueryOutOfTheContract_Returns400WithTheField(string query, string field)
    {
        using var response = await GetAsync($"/api/games?{query}", Guid.CreateVersion7());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadJsonAsync();
        Assert.Equal([field], problem.GetProperty("errors").EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public async Task Get_ReturnsTheGameWithItsMovesAndTimeControl()
    {
        var user = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();
        var gameId = await RecordNewAsync(User(user), User(bob, "bob"), _end);

        var game = await GetJsonAsync($"/api/games/{gameId}", bob);

        Assert.Equal(
            ["id", "variant", "white", "black", "result", "reason", "startedAt", "endedAt", "timeControl", "moves"],
            game.EnumerateObject().Select(property => property.Name));
        Assert.Equal(gameId, game.GetRequiredGuid("id"));
        AssertUser(game.GetProperty("white"), user, "alice");
        AssertUser(game.GetProperty("black"), bob, "bob");
        Assert.Equal(600000, game.GetProperty("timeControl").GetProperty("initialMs").GetInt64());
        Assert.Equal(
            ["e2e4", "e7e5", "d1h5", "b8c6", "f1c4", "g8f6", "h5f7"],
            game.GetProperty("moves").EnumerateArray().Select(move => move.GetString()));
    }

    [Fact]
    public async Task Get_OfAGameOfOtherUsers_Returns404LikeAMissingGame()
    {
        var gameId = await RecordNewAsync(User(Guid.CreateVersion7()), User(Guid.CreateVersion7(), "bob"), _end);
        var user = Guid.CreateVersion7();

        using var ofOthers = await GetAsync($"/api/games/{gameId}", user);
        using var missing = await GetAsync($"/api/games/{Guid.CreateVersion7()}", user);

        foreach (var response in new[] { ofOthers, missing })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var problem = await response.ReadJsonAsync();
            Assert.Equal("The game does not exist.", problem.GetRequiredString("detail"));
        }
    }

    [Fact]
    public async Task Get_WithAnIdThatIsNotAUuid_Returns400WithTheField()
    {
        using var response = await GetAsync("/api/games/not-a-uuid", Guid.CreateVersion7());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.ReadJsonAsync();
        Assert.Equal(["gameId"], problem.GetProperty("errors").EnumerateObject().Select(property => property.Name));
    }

    [Theory]
    [InlineData("/api/games")]
    [InlineData("/api/games/0193c2bf-0a1b-7c2d-9e3f-4a5b6c7d8e9f")]
    public async Task WithoutIdentityHeaders_Returns401(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<Guid> RecordNewAsync(PlayerDto white, PlayerDto black, DateTimeOffset endedAt, Guid? gameId = null)
    {
        var command = NewGame(white, black, endedAt, gameId);
        Assert.Equal(RecordOutcome.Recorded, await RecordAsync(factory, command));
        return command.GameId;
    }

    private async Task<JsonElement> GetJsonAsync(string path, Guid userId)
    {
        using var response = await GetAsync(path, userId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        return await response.ReadJsonAsync();
    }

    private async Task<HttpResponseMessage> GetAsync(string path, Guid userId)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("x-user-id", userId.ToString());
        request.Headers.Add("x-user-name", "alice");
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static void AssertUser(JsonElement player, Guid userId, string name)
    {
        Assert.Equal(["kind", "userId", "name"], player.EnumerateObject().Select(property => property.Name));
        Assert.Equal("user", player.GetRequiredString("kind"));
        Assert.Equal(userId, player.GetRequiredGuid("userId"));
        Assert.Equal(name, player.GetRequiredString("name"));
    }

    // The bot has no id, so its JSON leaves the field out (ADR-8).
    private static void AssertBot(JsonElement player)
    {
        Assert.Equal(["kind", "name"], player.EnumerateObject().Select(property => property.Name));
        Assert.Equal("bot", player.GetRequiredString("kind"));
        Assert.Equal("Stockfish bot", player.GetRequiredString("name"));
    }
}
