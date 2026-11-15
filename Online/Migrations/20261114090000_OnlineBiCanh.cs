using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VanDao.Data;

#nullable disable

namespace VanDao.Online.Migrations
{
    /// <summary>Thái Hư Bí Cảnh: trạng thái JSON (ngày, lượt đã vào, tầng sâu nhất, lượt đang chơi) trên Players.</summary>
    [DbContext(typeof(GameDbContext))]
    [Migration("20261114090000_OnlineBiCanh")]
    public partial class OnlineBiCanh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "BiCanhState", schema: "game", table: "Players", type: "text", nullable: false, defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "BiCanhState", schema: "game", table: "Players");
        }
    }
}
