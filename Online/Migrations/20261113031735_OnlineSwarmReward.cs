using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanDao.Online.Migrations
{
    /// <inheritdoc />
    public partial class OnlineSwarmReward : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SwarmClaims",
                schema: "game",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SwarmEntries",
                schema: "game",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SwarmDay",
                schema: "game",
                table: "Players",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SwarmEntries",
                schema: "game",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "SwarmClaims",
                schema: "game",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "SwarmDay",
                schema: "game",
                table: "Players");
        }
    }
}
