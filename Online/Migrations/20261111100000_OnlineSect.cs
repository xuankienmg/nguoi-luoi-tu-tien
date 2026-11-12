using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using VanDao.Data;

#nullable disable

namespace VanDao.Online.Migrations
{
    /// <summary>Tông Môn: bảng Sects, SectMembers, SectApplications; ChatMessages.SectId cho kênh tông.</summary>
    [DbContext(typeof(GameDbContext))]
    [Migration("20261111100000_OnlineSect")]
    public partial class OnlineSect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sects",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NameKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Fund = table.Column<long>(type: "bigint", nullable: false),
                    Notice = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AutoAccept = table.Column<bool>(type: "boolean", nullable: false),
                    MinRealm = table.Column<int>(type: "integer", nullable: false),
                    LeaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_Sects", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_Sects_NameKey", schema: "game", table: "Sects", column: "NameKey", unique: true);

            migrationBuilder.CreateTable(
                name: "SectMembers",
                schema: "game",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectId = table.Column<long>(type: "bigint", nullable: true),
                    Role = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Contribution = table.Column<long>(type: "bigint", nullable: false),
                    TotalContribution = table.Column<long>(type: "bigint", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeftAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckinDay = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DonateDay = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DonateCount = table.Column<int>(type: "integer", nullable: false),
                    ShopWeek = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ShopBought = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectMembers", x => x.PlayerId);
                    table.ForeignKey(name: "FK_SectMembers_Players_PlayerId", column: x => x.PlayerId,
                        principalSchema: "game", principalTable: "Players", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(name: "FK_SectMembers_Sects_SectId", column: x => x.SectId,
                        principalSchema: "game", principalTable: "Sects", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(name: "IX_SectMembers_SectId", schema: "game", table: "SectMembers", column: "SectId");

            migrationBuilder.CreateTable(
                name: "SectApplications",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SectId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectApplications", x => x.Id);
                    table.ForeignKey(name: "FK_SectApplications_Players_PlayerId", column: x => x.PlayerId,
                        principalSchema: "game", principalTable: "Players", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(name: "FK_SectApplications_Sects_SectId", column: x => x.SectId,
                        principalSchema: "game", principalTable: "Sects", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_SectApplications_PlayerId", schema: "game", table: "SectApplications", column: "PlayerId");
            migrationBuilder.CreateIndex(name: "IX_SectApplications_SectId_PlayerId", schema: "game", table: "SectApplications",
                columns: new[] { "SectId", "PlayerId" }, unique: true);

            migrationBuilder.AddColumn<long>(name: "SectId", schema: "game", table: "ChatMessages", type: "bigint", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_ChatMessages_SectId_Id", schema: "game", table: "ChatMessages", columns: new[] { "SectId", "Id" });
            migrationBuilder.AddForeignKey(name: "FK_ChatMessages_Sects_SectId", schema: "game", table: "ChatMessages", column: "SectId",
                principalSchema: "game", principalTable: "Sects", principalColumn: "Id", onDelete: ReferentialAction.Cascade);

            // bảng mới cần cùng quyền game_backend + RLS như các bảng khác
            migrationBuilder.Sql(OnlineSchema.Grants);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_ChatMessages_Sects_SectId", schema: "game", table: "ChatMessages");
            migrationBuilder.DropIndex(name: "IX_ChatMessages_SectId_Id", schema: "game", table: "ChatMessages");
            migrationBuilder.DropColumn(name: "SectId", schema: "game", table: "ChatMessages");
            migrationBuilder.DropTable(name: "SectApplications", schema: "game");
            migrationBuilder.DropTable(name: "SectMembers", schema: "game");
            migrationBuilder.DropTable(name: "Sects", schema: "game");
        }
    }
}
