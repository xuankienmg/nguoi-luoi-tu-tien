using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VanDao.Data;

#nullable disable

namespace VanDao.Online.Migrations
{
    /// <summary>Boss Tông Môn hằng ngày: bảng SectBosses, SectBossHits; Sects.BuffDay; SectMembers.BossRef.</summary>
    [DbContext(typeof(GameDbContext))]
    [Migration("20261111110000_OnlineSectBoss")]
    public partial class OnlineSectBoss : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "BuffDay", schema: "game", table: "Sects", type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<double>(name: "BossRef", schema: "game", table: "SectMembers", type: "double precision", nullable: false, defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "SectBosses",
                schema: "game",
                columns: table => new
                {
                    SectId = table.Column<long>(type: "bigint", nullable: false),
                    Day = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BossId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MaxHp = table.Column<double>(type: "double precision", nullable: false),
                    Damage = table.Column<double>(type: "double precision", nullable: false),
                    DefeatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectBosses", x => new { x.SectId, x.Day });
                    table.ForeignKey(name: "FK_SectBosses_Sects_SectId", column: x => x.SectId,
                        principalSchema: "game", principalTable: "Sects", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SectBossHits",
                schema: "game",
                columns: table => new
                {
                    SectId = table.Column<long>(type: "bigint", nullable: false),
                    Day = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hits = table.Column<int>(type: "integer", nullable: false),
                    Damage = table.Column<double>(type: "double precision", nullable: false),
                    ChestClaimed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectBossHits", x => new { x.SectId, x.Day, x.PlayerId });
                    table.ForeignKey(name: "FK_SectBossHits_Players_PlayerId", column: x => x.PlayerId,
                        principalSchema: "game", principalTable: "Players", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(name: "FK_SectBossHits_Sects_SectId", column: x => x.SectId,
                        principalSchema: "game", principalTable: "Sects", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_SectBossHits_PlayerId", schema: "game", table: "SectBossHits", column: "PlayerId");

            // bảng mới cần cùng quyền game_backend + RLS như các bảng khác
            migrationBuilder.Sql(OnlineSchema.Grants);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SectBossHits", schema: "game");
            migrationBuilder.DropTable(name: "SectBosses", schema: "game");
            migrationBuilder.DropColumn(name: "BossRef", schema: "game", table: "SectMembers");
            migrationBuilder.DropColumn(name: "BuffDay", schema: "game", table: "Sects");
        }
    }
}
