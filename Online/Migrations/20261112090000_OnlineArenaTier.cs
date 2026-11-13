using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VanDao.Data;

#nullable disable

namespace VanDao.Online.Migrations
{
    /// <summary>Hạng Lôi đài (Đồng/Bạc/…): điểm hạng, trận bảo vệ sau khi lên hạng, ngày / tuần đã nhận thưởng trên ArenaRanks.</summary>
    [DbContext(typeof(GameDbContext))]
    [Migration("20261112090000_OnlineArenaTier")]
    public partial class OnlineArenaTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "Points", schema: "game", table: "ArenaRanks", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "Protect", schema: "game", table: "ArenaRanks", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>(name: "DailyClaimDay", schema: "game", table: "ArenaRanks", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "WeeklyClaimWeek", schema: "game", table: "ArenaRanks", type: "text", nullable: false, defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var c in new[] { "Points", "Protect", "DailyClaimDay", "WeeklyClaimWeek" })
                migrationBuilder.DropColumn(name: c, schema: "game", table: "ArenaRanks");
        }
    }
}
