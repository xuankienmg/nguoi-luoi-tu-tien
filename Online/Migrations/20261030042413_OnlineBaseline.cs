using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VanDao.Online.Migrations
{
    /// <inheritdoc />
    public partial class OnlineBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "game");

            migrationBuilder.CreateTable(
                name: "OnlineRequests",
                schema: "game",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Response = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnlineRequests", x => new { x.OwnerId, x.RequestId });
                });

            migrationBuilder.CreateTable(
                name: "Players",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastChannelAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StarterChosen = table.Column<bool>(type: "boolean", nullable: false),
                    Realm = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    Qi = table.Column<double>(type: "double precision", nullable: false),
                    Bonus = table.Column<double>(type: "double precision", nullable: false),
                    PillBonus = table.Column<double>(type: "double precision", nullable: false),
                    Skin = table.Column<string>(type: "text", nullable: false),
                    Gender = table.Column<string>(type: "text", nullable: false),
                    SkinsUnlocked = table.Column<string>(type: "text", nullable: false),
                    AutoExplore = table.Column<bool>(type: "boolean", nullable: false),
                    ExploreReadyAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastTickAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Area = table.Column<int>(type: "integer", nullable: false),
                    AreaMax = table.Column<int>(type: "integer", nullable: false),
                    Chapter = table.Column<int>(type: "integer", nullable: false),
                    ChapterMax = table.Column<int>(type: "integer", nullable: false),
                    MapVer = table.Column<int>(type: "integer", nullable: false),
                    Diff = table.Column<int>(type: "integer", nullable: false),
                    DiffProg = table.Column<string>(type: "text", nullable: false),
                    Cleared = table.Column<string>(type: "text", nullable: false),
                    MapRealm = table.Column<int>(type: "integer", nullable: false),
                    BreakCooldownUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DebuffUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Skills = table.Column<string>(type: "text", nullable: false),
                    LearnedManuals = table.Column<string>(type: "text", nullable: false),
                    ManualVersion = table.Column<int>(type: "integer", nullable: false),
                    ManualProgress = table.Column<string>(type: "text", nullable: false),
                    ActiveManual = table.Column<string>(type: "text", nullable: false),
                    BossKills = table.Column<int>(type: "integer", nullable: false),
                    ElixirPoints = table.Column<int>(type: "integer", nullable: false),
                    SkillSlots = table.Column<string>(type: "text", nullable: false),
                    Admin = table.Column<string>(type: "text", nullable: false),
                    AdminPoints = table.Column<int>(type: "integer", nullable: false),
                    Redeemed = table.Column<string>(type: "text", nullable: false),
                    SpinHistory = table.Column<string>(type: "text", nullable: false),
                    SpinDay = table.Column<string>(type: "text", nullable: false),
                    SpinDailyUsed = table.Column<int>(type: "integer", nullable: false),
                    PetSpinDay = table.Column<string>(type: "text", nullable: false),
                    PetSpinDailyUsed = table.Column<int>(type: "integer", nullable: false),
                    SpinPityUsed = table.Column<int>(type: "integer", nullable: false),
                    PetSpinPityUsed = table.Column<int>(type: "integer", nullable: false),
                    EventKey = table.Column<string>(type: "text", nullable: false),
                    EventPoints = table.Column<int>(type: "integer", nullable: false),
                    EventClaimed = table.Column<string>(type: "text", nullable: false),
                    LbSecret = table.Column<string>(type: "text", nullable: false),
                    Tainted = table.Column<bool>(type: "boolean", nullable: false),
                    Flagged = table.Column<bool>(type: "boolean", nullable: false),
                    AlchemyExp = table.Column<int>(type: "integer", nullable: false),
                    RefineRecipe = table.Column<int>(type: "integer", nullable: false),
                    RefineStartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RefineEndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RefineLeft = table.Column<int>(type: "integer", nullable: false),
                    RefineCatalyst = table.Column<bool>(type: "boolean", nullable: false),
                    LastRefine = table.Column<string>(type: "text", nullable: false),
                    LastRefineAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BodyHp = table.Column<long>(type: "bigint", nullable: false),
                    BodyDef = table.Column<long>(type: "bigint", nullable: false),
                    Hatch = table.Column<string>(type: "text", nullable: false),
                    GemVault = table.Column<string>(type: "text", nullable: false),
                    GemSlots = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Equipment",
                schema: "game",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ItemId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => new { x.PlayerId, x.Slot });
                    table.ForeignKey(
                        name: "FK_Equipment_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inventory",
                schema: "game",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventory", x => new { x.PlayerId, x.ItemId });
                    table.ForeignKey(
                        name: "FK_Inventory_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Logs",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Logs_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pets",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefId = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Exp = table.Column<int>(type: "integer", nullable: false),
                    Form = table.Column<int>(type: "integer", nullable: false),
                    AppearanceForm = table.Column<int>(type: "integer", nullable: false),
                    Quality = table.Column<int>(type: "integer", nullable: false),
                    Stars = table.Column<int>(type: "integer", nullable: false),
                    StarPts = table.Column<int>(type: "integer", nullable: false),
                    AptAtk = table.Column<int>(type: "integer", nullable: false),
                    AptDef = table.Column<int>(type: "integer", nullable: false),
                    AptHp = table.Column<int>(type: "integer", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    FoodSpent = table.Column<int>(type: "integer", nullable: false),
                    Awaken = table.Column<int>(type: "integer", nullable: false),
                    GearCollar = table.Column<string>(type: "text", nullable: false),
                    GearArmor = table.Column<string>(type: "text", nullable: false),
                    GearCharm = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pets_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalSchema: "game",
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Logs_PlayerId_Id",
                schema: "game",
                table: "Logs",
                columns: new[] { "PlayerId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OnlineRequests_OwnerId_CreatedAt",
                schema: "game",
                table: "OnlineRequests",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Pets_PlayerId",
                schema: "game",
                table: "Pets",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_OwnerId",
                schema: "game",
                table: "Players",
                column: "OwnerId",
                unique: true,
                filter: "\"OwnerId\" IS NOT NULL");

            migrationBuilder.Sql(OnlineSchema.Security);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Equipment",
                schema: "game");

            migrationBuilder.DropTable(
                name: "Inventory",
                schema: "game");

            migrationBuilder.DropTable(
                name: "Logs",
                schema: "game");

            migrationBuilder.DropTable(
                name: "OnlineRequests",
                schema: "game");

            migrationBuilder.DropTable(
                name: "Pets",
                schema: "game");

            migrationBuilder.DropTable(
                name: "Players",
                schema: "game");
        }
    }
}
