using System.Text.Json.Nodes;

namespace PlatformService.IntegrationTests.Messaging;

/// <summary>The bodies of <c>game.finished.v1</c> for the tests, from the examples of <c>game-events.asyncapi.yaml</c>.</summary>
internal static class GameEvents
{
    public static readonly Guid Alice = Guid.Parse("0193c2b4-7d3e-7c2a-9f1e-3b5a6c7d8e9f");

    /// <summary>The example whiteWonByCheckmate of the contract.</summary>
    public const string WhiteWonByCheckmate = """
        {
          "id": "0193c2c0-1a2b-7c3d-8e4f-5a6b7c8d9e0f",
          "type": "game.finished.v1",
          "source": "game-service",
          "time": "2026-11-20T14:03:12.345Z",
          "data": {
            "gameId": "0193c2bf-0a1b-7c2d-9e3f-4a5b6c7d8e9f",
            "variant": "standard",
            "white": { "kind": "user", "userId": "0193c2b4-7d3e-7c2a-9f1e-3b5a6c7d8e9f", "name": "alice" },
            "black": { "kind": "user", "userId": "0193c2b5-1f2e-7d3c-8b4a-5c6d7e8f9a0b", "name": "bob" },
            "result": "whiteWon",
            "reason": "checkmate",
            "moves": ["e2e4", "e7e5", "d1h5", "b8c6", "f1c4", "g8f6", "h5f7"],
            "timeControl": { "initialMs": 600000 },
            "startedAt": "2026-11-20T14:01:40.120Z",
            "endedAt": "2026-11-20T14:03:12.345Z"
          }
        }
        """;

    /// <summary>The example botGameResigned of the contract.</summary>
    public const string BotGameResigned = """
        {
          "id": "0193c2d1-2b3c-7d4e-9f5a-6b7c8d9e0f1a",
          "type": "game.finished.v1",
          "source": "game-service",
          "time": "2026-11-20T15:10:05.000Z",
          "data": {
            "gameId": "0193c2cf-3c4d-7e5f-8a6b-7c8d9e0f1a2b",
            "variant": "standard",
            "white": { "kind": "bot", "name": "Stockfish bot" },
            "black": { "kind": "user", "userId": "0193c2b4-7d3e-7c2a-9f1e-3b5a6c7d8e9f", "name": "alice" },
            "result": "whiteWon",
            "reason": "resignation",
            "moves": ["e2e4", "c7c5"],
            "timeControl": { "initialMs": 600000 },
            "startedAt": "2026-11-20T15:09:30.000Z",
            "endedAt": "2026-11-20T15:10:05.000Z"
          }
        }
        """;

    /// <summary>
    /// The example whiteWonByCheckmate with new ids and a new user as white, because the tests share one database. The
    /// test may change the JSON before it is written.
    /// </summary>
    public static Event NewGame(Guid whiteUserId, Action<JsonObject>? change = null)
    {
        var (eventId, gameId) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var json = JsonNode.Parse(WhiteWonByCheckmate)!.AsObject();
        json["id"] = eventId.ToString();
        json["data"]!["gameId"] = gameId.ToString();
        json["data"]!["white"]!["userId"] = whiteUserId.ToString();
        change?.Invoke(json);
        return new Event(eventId, gameId, json.ToJsonString());
    }

    /// <summary>The body of an event with its ids.</summary>
    public sealed record Event(Guid EventId, Guid GameId, string Json);
}
