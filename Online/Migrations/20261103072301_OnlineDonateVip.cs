using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanDao.Online.Migrations
{
    /// <inheritdoc />
    public partial class OnlineDonateVip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Speed2",
                schema: "game",
                table: "Players",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Speed2On",
                schema: "game",
                table: "Players",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DonateCodes",
                schema: "game",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Vnd = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsedByPlayer = table.Column<Guid>(type: "uuid", nullable: true),
                    UsedByOwner = table.Column<Guid>(type: "uuid", nullable: true),
                    Offline = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonateCodes", x => x.Code);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DonateCodes_CreatedAt",
                schema: "game",
                table: "DonateCodes",
                column: "CreatedAt");

            // bảng mới cần cùng quyền game_backend + RLS như các bảng khác
            migrationBuilder.Sql(OnlineSchema.Grants);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DonateCodes",
                schema: "game");

            migrationBuilder.DropColumn(
                name: "Speed2",
                schema: "game",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Speed2On",
                schema: "game",
                table: "Players");
        }
    }
}
