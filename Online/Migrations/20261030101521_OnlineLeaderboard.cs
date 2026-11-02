using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanDao.Online.Migrations
{
    /// <inheritdoc />
    public partial class OnlineLeaderboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeaderboardEntries",
                schema: "game",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Realm = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Qi = table.Column<double>(type: "double precision", nullable: false),
                    RealmName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StageName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Power = table.Column<long>(type: "bigint", nullable: false),
                    Progress = table.Column<long>(type: "bigint", nullable: false),
                    PetPower = table.Column<long>(type: "bigint", nullable: false),
                    Equipment = table.Column<string>(type: "text", nullable: false),
                    Stats = table.Column<string>(type: "text", nullable: false),
                    Flagged = table.Column<bool>(type: "boolean", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardEntries", x => x.PlayerId);
                    table.ForeignKey(
                        name: "FK_LeaderboardEntries_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // bảng mới cần cùng quyền game_backend + RLS như các bảng khác
            migrationBuilder.Sql(OnlineSchema.Grants);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaderboardEntries",
                schema: "game");
        }
    }
}
