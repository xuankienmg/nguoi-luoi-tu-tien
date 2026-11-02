using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VanDao.Online.Migrations
{
    /// <inheritdoc />
    public partial class OnlineMarket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketListings",
                schema: "game",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ItemId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Qty = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: true),
                    BuyerName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Payout = table.Column<long>(type: "bigint", nullable: false),
                    SellerClaimed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketListings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketListings_Players_SellerId",
                        column: x => x.SellerId,
                        principalSchema: "game",
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketListings_BuyerId_ClosedAt",
                schema: "game",
                table: "MarketListings",
                columns: new[] { "BuyerId", "ClosedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketListings_SellerId_Status",
                schema: "game",
                table: "MarketListings",
                columns: new[] { "SellerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketListings_Status_CreatedAt",
                schema: "game",
                table: "MarketListings",
                columns: new[] { "Status", "CreatedAt" });

            // bảng mới cần cùng quyền game_backend + RLS như các bảng khác
            migrationBuilder.Sql(OnlineSchema.Grants);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketListings",
                schema: "game");
        }
    }
}
