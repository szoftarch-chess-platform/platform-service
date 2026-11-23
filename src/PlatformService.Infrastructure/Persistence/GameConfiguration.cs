using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlatformService.Domain.Games;

namespace PlatformService.Infrastructure.Persistence;

/// <summary>
/// The <c>games</c> table of PS-ADR-13. The players and the time control are complex types in columns of the same
/// table, and the moves are a <c>text[]</c> column.
/// </summary>
/// <remarks>
/// EF Core 10 cannot index a property of a complex type, so the migration creates the indexes of
/// <c>white_user_id</c> and <c>black_user_id</c> with SQL, and they are not in this model.
/// </remarks>
internal sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    /// <summary>The shadow property with the id of the event that reported the game.</summary>
    public const string EventIdProperty = "EventId";

    /// <summary>The primary key, which <see cref="UnitOfWork"/> recognizes at the commit.</summary>
    public const string PrimaryKey = "pk_games";

    /// <summary>The unique index of the event id, which <see cref="UnitOfWork"/> recognizes at the commit.</summary>
    public const string EventIdIndex = "ix_games_event_id";

    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("games");

        // The game-service creates the ids (ADR-8).
        builder.HasKey(game => game.Id).HasName(PrimaryKey);
        builder.Property(game => game.Id).ValueGeneratedNever();

        // The id of the event stays out of the Domain, and the repository sets it (ADR-6).
        builder.Property<Guid>(EventIdProperty);
        builder.HasIndex(EventIdProperty).IsUnique().HasDatabaseName(EventIdIndex);

        // Strings and not enums, so a new value of the contract passes through (ADR-7).
        builder.Property(game => game.Variant);
        builder.Property(game => game.Result);
        builder.Property(game => game.Reason);

        builder.ComplexProperty(game => game.White, ConfigurePlayer);
        builder.ComplexProperty(game => game.Black, ConfigurePlayer);
        builder.ComplexProperty(game => game.TimeControl);

        builder.Property(game => game.Moves);
        builder.Property(game => game.StartedAt);
        builder.Property(game => game.EndedAt);
    }

    /// <summary>The Domain sets the properties of a player only in its constructor, so they are mapped explicitly.</summary>
    private static void ConfigurePlayer(ComplexPropertyBuilder<Player> player)
    {
        player.Property(value => value.Kind);
        player.Property(value => value.UserId);
        player.Property(value => value.Name);
    }
}
