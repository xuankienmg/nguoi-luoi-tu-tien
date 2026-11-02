using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VanDao.Online.Migrations
{
    /// <inheritdoc />
    public partial class OnlineArena : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Loadout",
                schema: "game",
                table: "LeaderboardEntries",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArenaFights",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttackerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttackerName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DefenderName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Win = table.Column<bool>(type: "boolean", nullable: false),
                    AttackerRank = table.Column<long>(type: "bigint", nullable: false),
                    DefenderRank = table.Column<long>(type: "bigint", nullable: false),
                    Swapped = table.Column<bool>(type: "boolean", nullable: false),
                    Seed = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Practice = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArenaFights", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArenaRanks",
                schema: "game",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pos = table.Column<long>(type: "bigint", nullable: false),
                    Wins = table.Column<int>(type: "integer", nullable: false),
                    Losses = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArenaRanks", x => x.PlayerId);
                    table.ForeignKey(
                        name: "FK_ArenaRanks_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArenaFights_AttackerId_CreatedAt",
                schema: "game",
                table: "ArenaFights",
                columns: new[] { "AttackerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArenaFights_DefenderId_CreatedAt",
                schema: "game",
                table: "ArenaFights",
                columns: new[] { "DefenderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArenaRanks_Pos",
                schema: "game",
                table: "ArenaRanks",
                column: "Pos",
                unique: true);

            // bảng mới cần cùng quyền game_backend + RLS như các bảng khác
            migrationBuilder.Sql(OnlineSchema.Grants);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArenaFights",
                schema: "game");

            migrationBuilder.DropTable(
                name: "ArenaRanks",
                schema: "game");

            migrationBuilder.DropColumn(
                name: "Loadout",
                schema: "game",
                table: "LeaderboardEntries");
        }
    }
}
