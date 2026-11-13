using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VanDao.Data;

#nullable disable

namespace VanDao.Online.Migrations
{
    /// <summary>Cảnh giới riêng của linh thú (Pet.Realm): -1 = chưa gán, gán lúc nạp theo cảnh giới chủ nhân.</summary>
    [DbContext(typeof(GameDbContext))]
    [Migration("20261110090000_OnlinePetRealm")]
    public partial class OnlinePetRealm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Realm",
                schema: "game",
                table: "Pets",
                type: "integer",
                nullable: false,
                defaultValue: -1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Realm",
                schema: "game",
                table: "Pets");
        }
    }
}
