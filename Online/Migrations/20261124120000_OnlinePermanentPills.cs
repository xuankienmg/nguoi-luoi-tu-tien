using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VanDao.Data;

namespace VanDao.Online.Migrations;

[DbContext(typeof(GameDbContext))]
[Migration("20261124120000_OnlinePermanentPills")]
public class OnlinePermanentPills : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(name: "PermanentPills", schema: "game", table: "Players", type: "text", nullable: false, defaultValue: "");
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(name: "PermanentPills", schema: "game", table: "Players");
}
