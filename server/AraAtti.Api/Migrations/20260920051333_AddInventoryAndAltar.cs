using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AraAtti.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAndAltar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "altar_contributions",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    request_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    amount = table.Column<uint>(type: "int unsigned", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_altar_contributions", x => x.id);
                    table.ForeignKey(
                        name: "fk_contributions_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "altar_state",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    total_offered = table.Column<ulong>(type: "bigint unsigned", nullable: false, defaultValue: 0ul),
                    target_offering = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 1000u),
                    activated_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_altar_state", x => x.id);
                    table.CheckConstraint("ck_altar_target_positive", "`target_offering` > 0");
                    table.CheckConstraint("ck_altar_total_le_target", "`total_offered` <= `target_offering`");
                    table.CheckConstraint("ck_altar_total_nonneg", "`total_offered` >= 0");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "player_inventories",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    item_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    quantity = table.Column<uint>(type: "int unsigned", nullable: false, defaultValue: 0u),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_inventories", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "reward_claims",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    game_id = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    match_key = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    claimed_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reward_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_claims_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "altar_state",
                columns: new[] { "id", "activated_at", "target_offering", "updated_at" },
                values: new object[] { 1, null, 1000u, new DateTime(2026, 9, 20, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "idx_contributions_user",
                table: "altar_contributions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uk_contributions_user_request",
                table: "altar_contributions",
                columns: new[] { "user_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uk_inventory_user_item",
                table: "player_inventories",
                columns: new[] { "user_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_claims_user_time",
                table: "reward_claims",
                columns: new[] { "user_id", "claimed_at" });

            migrationBuilder.CreateIndex(
                name: "uk_claims_user_match",
                table: "reward_claims",
                columns: new[] { "user_id", "match_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "altar_contributions");

            migrationBuilder.DropTable(
                name: "altar_state");

            migrationBuilder.DropTable(
                name: "player_inventories");

            migrationBuilder.DropTable(
                name: "reward_claims");
        }
    }
}
