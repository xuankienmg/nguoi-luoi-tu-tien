using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VanDao.Online.Migrations
{
    /// <inheritdoc />
    public partial class OnlinePlayerXmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "game",
                table: "Players",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "game",
                table: "Players");
        }
    }
}
