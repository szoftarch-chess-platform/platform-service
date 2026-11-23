# PS-ADR-13: Game history

**Related:** F-6, NF-3, NF-4, NF-6, ADR-6, ADR-7, ADR-8, ADR-10, [PS-ADR-1](clean-architecture.md), [PS-ADR-2](http-api.md), [PS-ADR-5](error-handling.md), [PS-ADR-6](validation.md), [PS-ADR-7](persistence.md), [PS-ADR-8](configuration-and-logging.md), [PS-ADR-10](testing.md), [PS-ADR-12](tokens-and-sessions.md)

## Context

The game-service hands every finished game over in the event `game.finished.v1` of `game-events.asyncapi.yaml`, the games against the bot and the aborted games included (ADR-6, ADR-10). The platform-service keeps the games, so that a user can list their games and replay one with its moves (F-6). NF-3 requires that the service can store the last 100 games of each user. The contract `platform-service.openapi.yaml` lists the games of the signed-in user page by page with the newest first, and it returns one game with its moves. ADR-7 asks every reader to handle a new value of an enum without failing, and the contracts are still young.

## Decision

- **Aggregate:** the `Game` of the Domain is a finished game with its variant, its two players, its result and its reason, its moves, its time control, its start and its end. `Game.Record` creates it with the times in UTC, and `IsPlayedBy` tells whether a user played in it with either color. Its repository is `IGameRepository` (PS-ADR-7).
- **Players:** a `Player` is a value object with a kind, a name and the id of the user. Only a player of the kind `user` keeps the id, so the bot or a kind of a later contract never puts a game into the history of a user.
- **Values as strings:** the variant, the result, the reason and the kind of a player are strings in the Domain, in the database and in the responses, not C# enums. A value that a later version of a contract adds passes through unchanged (ADR-7).
- **Table:** one `games` table with the id of the game as its primary key. The players and the time control are complex types of EF Core in columns of the same table, such as `white_kind`, `white_user_id` and `white_name`. The moves are a `text[]` column in the order of the game, and the start and the end are `timestamptz` columns.
- **Indexes:** `white_user_id` and `black_user_id` have an index each for the games of a user. EF Core 10 cannot index a property of a complex type, so the migration creates the two indexes with SQL, and they are not in the model.
- **Event:** the table keeps the id of the event that reported the game with a unique index, so a repeated delivery is found by it, as ADR-6 decides. The id is a shadow property that the repository sets, so the Domain knows nothing about the messages.
- **Every game stays:** the service deletes no game, so it keeps more than the 100 games of NF-3.
- **Recording:** `IFinishedGameService.RecordAsync` stores a game with the id of its event, and the consumer of `game.finished.v1` calls it. Its validator checks the rules of the schema of the event, such as the UCI pattern of every move and the id of every player of the kind `user`. A game whose id or event is stored already is not stored again, and the outcome says so. If two deliveries of the same game pass this check at the same time, the primary key or the unique index refuses the second commit. `IUnitOfWork` reports this as `GameAlreadyRecordedException`, and the use case takes it as a game that is stored already.
- **List:** `GET /api/games` goes from `GamesController` to `IGameHistoryService.ListAsync`. `page` is 1 and `pageSize` is 20 by default, and the validator allows a page of at least 1 and a page size of 1 to 100, as the contract does. The games of the user are ordered by `ended_at` and then by the id, both descending, and OFFSET and LIMIT cut the page. `totalCount` counts every game of the user. The service compares the offset with the count as a long and does not query the games of a page after the last one, which is empty.
- **One game:** `GET /api/games/{gameId}` goes to `IGameHistoryService.GetAsync`. The route has no constraint, so an id that is not a UUID fails the binding and gets the 400 of `ApiController`. A game that does not exist and a game that the user did not play both get 404 with the same `detail` (PS-ADR-5).
- **Responses:** the summary, the details, the player and the time control have DTOs of their own, mapped by hand as PS-ADR-1 decides. The JSON of the bot has no `userId`, because a missing value is left out (PS-ADR-2). The timestamps end in `+00:00`, which ADR-8 allows.

## Rationale

- A game always has exactly two players, so their columns keep a game in one row, and the list needs no join.
- EF Core 8 introduced complex types for value objects. They have no key and no identity, unlike owned types, which are entities with a hidden key. EF Core 11 can index a property of a complex type, but it needs .NET 11, and PS-ADR-1 keeps the Microsoft packages on 10.0.x.
- The list asks for `white_user_id = @user OR black_user_id = @user`, which PostgreSQL answers by combining the two indexes in a bitmap. The bitmap loses the order of an index, so `ended_at` in the indexes would not save the sort. The games of one user are few enough to sort in memory, also with the 1,000 users of NF-6, so the answer stays well within NF-4.
- An array keeps the moves in order, and each element is a text like a UCI move. Npgsql maps it to a list without a converter. JSONB would add a structure that a list of strings does not need, and the service always reads the moves of a game whole.
- With strings a new value, such as a new reason of ADR-10, is stored and shown. A C# enum would refuse the event, and the message would end in the dead-letter queue.
- Npgsql writes only UTC into a `timestamptz` column, while a reader accepts any offset (ADR-8), so the Domain converts the times when it records a game (PS-ADR-8).
- The game-service creates the id of a game as a UUID v7 when the game starts (ADR-8), so the id is known before the insert, and the service needs no id of its own.
- A repeated delivery usually comes after the commit of the first one, so the check before the insert finds it without a failed insert, which EF Core and PostgreSQL would log as an error. The unique indexes cover the rare parallel delivery.
- A game that is stored already under another event is the same finished game, so it is not stored twice either.
- With a shadow property the event stays out of the Domain, like the session of a refresh token (PS-ADR-12).
- The rules of a command are in the Application (PS-ADR-6), so the consumer only maps the event, and a game that the validator refuses goes to the dead-letter queue.
- Reading the history needs the current user and no commit, while recording needs the validator of the event and the Unit of Work and runs without a user. With two use cases every class has only the dependencies that it uses, as in PS-ADR-12.
- A game belongs to both of its players, so deleting the old games of one user would also take them from the history of the other user. A game is one row of a few hundred bytes, and keeping every game needs no cleanup.
- The same 404 for a missing game and for a game of other users tells nothing about the games of others. RFC 9110 allows a 404 instead of a 403 to hide that a resource exists.
- The contract counts pages with `page` and `totalCount`, which is offset pagination. A user has few games, so a deep OFFSET costs little, and the id as the second key keeps two games that ended at the same time in one order, so the pages do not overlap.
- The contract has no upper limit for `page`, and the offset of a large page number does not fit into an int.

## Rejected alternatives

- **Owned types for the players:** the indexes would be in the model, but an owned type is an entity with a hidden key, while complex types are meant for value objects.
- **A `game_players` table:** one index without OR, but two rows per game, a join in every query and a list of players in the Domain.
- **The moves in a JSONB column:** a structure that a list of strings does not need.
- **C# enums for the values:** a new value of a contract would refuse the event.
- **Keeping only the last 100 games of a user:** the cleanup would also delete games from the history of their other player.
- **A table of the processed events:** the general form of the idempotent consumer, but the service consumes one event, and the row of the game already records its event.
- **The route constraint `{gameId:guid}`:** an invalid id would get 404 instead of the 400 of the contract.
- **Keyset pagination:** steady on deep pages, but the contract counts pages by number.

## Consequences

- The indexes of the two user ids are not in the EF Core model. A migration that changes these columns takes care of them, and with EF Core 11 they move into the model with `HasIndex`.
- EF Core cannot bind complex types to the parameters of a constructor, so `Game` has a second private constructor for EF Core without the players and the time control.
- The table grows by one row for every finished game.
- The frontend gets a new value of an enum unchanged and has to show it (ADR-7).
- To do: the consumer of `game.finished.v1` maps the event to `RecordFinishedGameCommand` and calls `RecordAsync`.

## Sources

- EF Core 8, value objects using complex types: https://learn.microsoft.com/ef/core/what-is-new/ef-core-8.0/whatsnew#value-objects-using-complex-types
- EF Core indexes, with complex types from EF Core 11: https://learn.microsoft.com/ef/core/modeling/indexes
- EF Core migrations, adding raw SQL: https://learn.microsoft.com/ef/core/managing-schemas/migrations/managing#adding-raw-sql
- Npgsql, dates and times: https://www.npgsql.org/doc/types/datetime.html
- Npgsql EF Core, arrays: https://www.npgsql.org/efcore/mapping/array.html
- PostgreSQL 18, combining multiple indexes: https://www.postgresql.org/docs/18/indexes-bitmap-scans.html
- PostgreSQL 18, arrays: https://www.postgresql.org/docs/18/arrays.html
- RFC 9110, 403 Forbidden: https://www.rfc-editor.org/rfc/rfc9110#section-15.5.4
- Idempotent Consumer pattern: https://microservices.io/patterns/communication-style/idempotent-consumer.html
