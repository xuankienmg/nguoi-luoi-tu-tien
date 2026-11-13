using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VanDao.Data;

#nullable disable

namespace VanDao.Online.Migrations
{
    /// <summary>Nhiệm vụ ngày/tuần (Hoạt Lực): bộ đếm ngày, rương ngày, điểm và rương tuần trên Players.</summary>
    [DbContext(typeof(GameDbContext))]
    [Migration("20261111090000_OnlineDailyQuest")]
    public partial class OnlineDailyQuest : Migration
    {
        private static readonly string[] Texts = { "DqDay", "DqCounts", "DqWeek" };
        private static readonly string[] Ints = { "DqClaimed", "DqWeekPoints", "DqWeekClaimed" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var c in Texts)
                migrationBuilder.AddColumn<string>(name: c, schema: "game", table: "Players", type: "text", nullable: false, defaultValue: "");
            foreach (var c in Ints)
                migrationBuilder.AddColumn<int>(name: c, schema: "game", table: "Players", type: "integer", nullable: false, defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var c in Texts.Concat(Ints))
                migrationBuilder.DropColumn(name: c, schema: "game", table: "Players");
        }
    }
}
