using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlatformService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant = table.Column<string>(type: "text", nullable: false),
                    result = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    moves = table.Column<string[]>(type: "text[]", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    black_kind = table.Column<string>(type: "text", nullable: false),
                    black_name = table.Column<string>(type: "text", nullable: false),
                    black_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    time_control_initial_ms = table.Column<long>(type: "bigint", nullable: false),
                    white_kind = table.Column<string>(type: "text", nullable: false),
                    white_name = table.Column<string>(type: "text", nullable: false),
                    white_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_games", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_games_event_id",
                table: "games",
                column: "event_id",
                unique: true);

            // EF Core 10 cannot index a property of a complex type, so these two indexes are not in the model
            // (PS-ADR-13). Dropping the table drops them too.
            migrationBuilder.Sql("CREATE INDEX ix_games_white_user_id ON games (white_user_id);");
            migrationBuilder.Sql("CREATE INDEX ix_games_black_user_id ON games (black_user_id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "games");
        }
    }
}
