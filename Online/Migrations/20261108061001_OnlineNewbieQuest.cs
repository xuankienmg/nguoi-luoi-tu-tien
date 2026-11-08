using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanDao.Online.Migrations
{
    /// <inheritdoc />
    public partial class OnlineNewbieQuest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NewbieQuestCount",
                schema: "game",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NewbieQuestStep",
                schema: "game",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Người đã Trúc Cơ trở lên coi như xong nhiệm vụ tân thủ (NewbieQuestData.Total = 14).
            migrationBuilder.Sql("UPDATE game.\"Players\" SET \"NewbieQuestStep\" = 14 WHERE \"Realm\" >= 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewbieQuestCount",
                schema: "game",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "NewbieQuestStep",
                schema: "game",
                table: "Players");
        }
    }
}
